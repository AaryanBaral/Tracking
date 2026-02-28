using Agent.Service.Infrastructure;
using Agent.Service.Identity;
using Agent.Shared.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Service.Tracking;

public sealed class AppSessionizer
{
    private readonly IOutboxService _outbox;
    private readonly DeviceIdentityStore _identityStore;
    private readonly ILogger<AppSessionizer> _logger;
    private ActiveAppSession? _active;
    private readonly object _lock = new();
    private readonly TimeSpan _checkpointInterval;
    private readonly TimeSpan _endGrace;

    public AppSessionizer(
        IOutboxService outbox,
        DeviceIdentityStore identityStore,
        ILogger<AppSessionizer> logger,
        IOptions<AgentConfig> options)
    {
        _outbox = outbox;
        _identityStore = identityStore;
        _logger = logger;
        var checkpointSeconds = Math.Max(0, options.Value.AppSessionCheckpointSeconds);
        _checkpointInterval = checkpointSeconds > 0
            ? TimeSpan.FromSeconds(checkpointSeconds)
            : TimeSpan.Zero;
        var pollSeconds = ResolvePollSeconds(options.Value);
        _endGrace = TimeSpan.FromSeconds(Math.Max(1, pollSeconds * 2));
    }

    public async Task HandleAppFocusAsync(string appName, DateTimeOffset timestamp, string? windowTitle = null)
    {
        var identity = await _identityStore.GetOrCreateAsync(CancellationToken.None);
        var deviceId = identity.DeviceId.ToString();

        // Fire-and-forget outbox enqueue to avoid blocking the collector loop too long?
        // Actually, CollectorWorker awaits execution, so we should await here to propagate pressure/errors appropriately.
        // We will return Task.

        AppSessionRecord? toEnqueue = null;
        AppSessionRecord? checkpointToEnqueue = null;
        string? startApp = null;
        string? startTitle = null;
        DateTimeOffset startAt = default;
        var handledSameApp = false;

        lock (_lock)
        {
            if (_active is not null && string.Equals(_active.AppName, appName, StringComparison.OrdinalIgnoreCase))
            {
                // Same app, just update LastSeen
                handledSameApp = true;
                _active.LastSeenUtc = timestamp;
                if (!string.IsNullOrWhiteSpace(windowTitle))
                {
                    _active.WindowTitle = windowTitle;
                }
                if (_checkpointInterval > TimeSpan.Zero &&
                    timestamp - _active.StartUtc >= _checkpointInterval)
                {
                    var end = timestamp < _active.StartUtc ? DateTimeOffset.UtcNow : timestamp;
                    checkpointToEnqueue = new AppSessionRecord(
                        Guid.NewGuid(),
                        _active.DeviceId,
                        _active.AppName,
                        _active.WindowTitle,
                        _active.StartUtc,
                        end);

                    _active = new ActiveAppSession
                    {
                        DeviceId = _active.DeviceId,
                        AppName = _active.AppName,
                        WindowTitle = _active.WindowTitle,
                        StartUtc = end,
                        LastSeenUtc = end
                    };
                }
            }
            else if (_active is not null)
            {
                // Different app, close previous
                var end = timestamp;
                if (_active.LastSeenUtc > DateTimeOffset.MinValue)
                {
                    var gap = timestamp - _active.LastSeenUtc;
                    if (gap > _endGrace)
                    {
                        end = _active.LastSeenUtc;
                    }
                }
                if (end < _active.StartUtc) end = DateTimeOffset.UtcNow; // clock skew protection

                toEnqueue = new AppSessionRecord(
                    Guid.NewGuid(),
                    _active.DeviceId,
                    _active.AppName,
                    _active.WindowTitle,
                    _active.StartUtc,
                    end
                );
            }

            if (!handledSameApp)
            {
                // Start new
                _active = new ActiveAppSession
                {
                    DeviceId = deviceId,
                    AppName = appName,
                    WindowTitle = windowTitle,
                    StartUtc = timestamp,
                    LastSeenUtc = timestamp
                };
                startApp = appName;
                startTitle = windowTitle;
                startAt = timestamp;
            }
        }

        if (handledSameApp)
        {
            if (checkpointToEnqueue is not null)
            {
                await _outbox.EnqueueAsync("app_session", checkpointToEnqueue);
            }
            return;
        }

        if (startApp is not null)
        {
            _logger.LogInformation("APP START: {app} | {title} @ {start}", startApp, startTitle, startAt);
        }

        if (toEnqueue is not null)
        {
            await _outbox.EnqueueAsync("app_session", toEnqueue);
            _logger.LogInformation(
                "APP END: {app} | {title} {start} -> {end} (secs={secs:n0})",
                toEnqueue.AppName,
                toEnqueue.WindowTitle,
                toEnqueue.StartAtUtc,
                toEnqueue.EndAtUtc,
                (toEnqueue.EndAtUtc - toEnqueue.StartAtUtc).TotalSeconds);
        }
    }

    public async Task CloseActiveAsync()
    {
        AppSessionRecord? toEnqueue = null;
        lock (_lock)
        {
            if (_active is null) return;

            var now = DateTimeOffset.UtcNow;
            toEnqueue = new AppSessionRecord(
                Guid.NewGuid(),
                _active.DeviceId,
                _active.AppName,
                _active.WindowTitle,
                _active.StartUtc,
                now
            );
            _active = null;
        }

        if (toEnqueue is not null)
        {
            await _outbox.EnqueueAsync("app_session", toEnqueue);
            _logger.LogInformation(
                "APP END: {app} | {title} {start} -> {end} (secs={secs:n0})",
                toEnqueue.AppName,
                toEnqueue.WindowTitle,
                toEnqueue.StartAtUtc,
                toEnqueue.EndAtUtc,
                (toEnqueue.EndAtUtc - toEnqueue.StartAtUtc).TotalSeconds);
        }
    }

    private static int ResolvePollSeconds(AgentConfig config)
    {
        var pollSeconds = config.CollectorPollSeconds;
        var raw = Environment.GetEnvironmentVariable("AGENT_POLL_SECONDS");
        if (int.TryParse(raw, out var parsed) && parsed > 0)
        {
            pollSeconds = parsed;
        }

        return Math.Max(1, pollSeconds);
    }
}
