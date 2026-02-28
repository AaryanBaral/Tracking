using Agent.Service.Infrastructure;
using Agent.Shared.Config;
using Agent.Shared.Models;
using Microsoft.Extensions.Options;

namespace Agent.Service.Tracking;

public sealed class WebSessionizer
{
    private readonly IOutboxService _outbox;
    private readonly ILogger<WebSessionizer> _logger;
    private readonly object _lock = new();
    private ActiveWebSession? _active;
    private CurrentTab? _currentTab;
    private string? _deviceId;
    private DateTimeOffset? _lastBrowserActiveAt;
    private DateTimeOffset? _lastUserActiveAt;
    private bool _isBrowserForeground;
    private bool _hasAppFocusSignal;
    private readonly TimeSpan _foregroundGrace;
    private readonly TimeSpan _idleGrace;
    private readonly TimeSpan _checkpointInterval;

    private const double IdleThresholdSeconds = 60.0;
    private static readonly TimeSpan MinSessionDuration = TimeSpan.FromSeconds(1);

    public WebSessionizer(IOutboxService outbox, ILogger<WebSessionizer> logger, IOptions<AgentConfig> options)
    {
        _outbox = outbox;
        _logger = logger;
        var config = options.Value;
        var pollSeconds = ResolvePollSeconds(config);
        var graceSeconds = Math.Max(3, pollSeconds * 2);
        _foregroundGrace = TimeSpan.FromSeconds(graceSeconds);
        _idleGrace = TimeSpan.FromSeconds(graceSeconds);
        var checkpointSeconds = Math.Max(0, config.WebSessionCheckpointSeconds);
        _checkpointInterval = checkpointSeconds > 0
            ? TimeSpan.FromSeconds(checkpointSeconds)
            : TimeSpan.Zero;
    }

    public async Task HandleEventAsync(WebEvent evt, string deviceId)
    {
        var ts = evt.Timestamp == default ? DateTimeOffset.UtcNow : evt.Timestamp;
        
        WebSessionRecord? sessionToClose = null;

        lock (_lock)
        {
            _deviceId = deviceId;
            var browser = string.IsNullOrWhiteSpace(evt.Browser) ? "chromium" : evt.Browser;
            _currentTab = new CurrentTab(evt.Domain, evt.Url, evt.Title, browser, ts);

            var canUseEventAsActivity = !_hasAppFocusSignal || _isBrowserForeground;
            if (canUseEventAsActivity)
            {
                _lastBrowserActiveAt = ts;
                _lastUserActiveAt = ts;
                if (!_hasAppFocusSignal)
                {
                    _isBrowserForeground = true;
                }
                sessionToClose = EvaluateSessionLocked(ts);
            }
        }

        await _outbox.EnqueueAsync("web_event", evt);

        await EnqueueIfValidAsync(sessionToClose);
    }

    public async Task HandleAppFocusAsync(string appName, DateTimeOffset timestamp)
    {
        WebSessionRecord? sessionToClose = null;
        lock (_lock)
        {
            _hasAppFocusSignal = true;
            _lastUserActiveAt = timestamp;
            if (IsBrowserApp(appName))
            {
                _isBrowserForeground = true;
                _lastBrowserActiveAt = timestamp;
                sessionToClose = EvaluateSessionLocked(timestamp);
            }
            else
            {
                _isBrowserForeground = false;
                _lastBrowserActiveAt = null;
                _currentTab = null;
                sessionToClose = CloseActiveLocked(timestamp);
            }
        }

        await EnqueueIfValidAsync(sessionToClose);
    }

    public async Task HandleIdleStateAsync(TimeSpan idleDuration, DateTimeOffset timestamp)
    {
        WebSessionRecord? sessionToClose = null;
        lock (_lock)
        {
            if (idleDuration.TotalSeconds < IdleThresholdSeconds)
            {
                _lastUserActiveAt = timestamp;
            }

            sessionToClose = EvaluateSessionLocked(timestamp);
        }

        await EnqueueIfValidAsync(sessionToClose);
    }

    public async Task CloseActiveAsync()
    {
        WebSessionRecord? sessionToClose = null;
        lock (_lock)
        {
            if (_active is null) return;

            var now = DateTimeOffset.UtcNow;
            var duration = now - _active.StartUtc;
            if (duration >= MinSessionDuration)
            {
                _logger.LogInformation(
                    "WEB END: {browser} {domain} {start} -> {end} (secs={secs:n0})",
                    _active.Browser,
                    _active.Domain,
                    _active.StartUtc,
                    now,
                    duration.TotalSeconds);
            }
            sessionToClose = new WebSessionRecord(
                Guid.NewGuid(),
                _active.DeviceId,
                _active.Domain,
                _active.Title,
                _active.Url,
                _active.StartUtc,
                now);
            
            _active = null;
        }

        await EnqueueIfValidAsync(sessionToClose);
    }

    private async Task EnqueueIfValidAsync(WebSessionRecord? sessionToClose)
    {
        if (sessionToClose is null)
        {
            return;
        }

        if ((sessionToClose.EndAtUtc - sessionToClose.StartAtUtc) < MinSessionDuration)
        {
            return;
        }

        await _outbox.EnqueueAsync("web_session", sessionToClose);
    }

    private WebSessionRecord? EvaluateSessionLocked(DateTimeOffset now)
    {
        var isActiveWindow = IsBrowserActive(now);
        var isUserActive = IsUserActive(now);
        var hasTab = _currentTab is not null && !string.IsNullOrWhiteSpace(_currentTab.Domain);
        var shouldBeActive = isActiveWindow && isUserActive && hasTab;

        if (shouldBeActive && _currentTab is not null && !string.IsNullOrWhiteSpace(_deviceId))
        {
            if (_active is null)
            {
                _active = new ActiveWebSession
                {
                    DeviceId = _deviceId,
                    Browser = _currentTab.Browser,
                    Domain = _currentTab.Domain,
                    Url = _currentTab.Url,
                    Title = _currentTab.Title,
                    StartUtc = now,
                    LastSeenUtc = now
                };
                _logger.LogInformation(
                    "WEB START: {browser} {domain} {url} @ {start}",
                    _active.Browser,
                    _active.Domain,
                    _active.Url,
                    now);
                return null;
            }

            if (!IsSamePage(_active, _currentTab))
            {
                var end = now < _active.StartUtc ? DateTimeOffset.UtcNow : now;
                var duration = end - _active.StartUtc;
                if (duration >= MinSessionDuration)
                {
                    _logger.LogInformation(
                        "WEB END: {browser} {domain} {start} -> {end} (secs={secs:n0})",
                        _active.Browser,
                        _active.Domain,
                        _active.StartUtc,
                        end,
                        duration.TotalSeconds);
                }
                var sessionToClose = new WebSessionRecord(
                    Guid.NewGuid(),
                    _active.DeviceId,
                    _active.Domain,
                    _active.Title,
                    _active.Url,
                    _active.StartUtc,
                    end);

                _active = new ActiveWebSession
                {
                    DeviceId = _active.DeviceId,
                    Browser = _currentTab.Browser,
                    Domain = _currentTab.Domain,
                    Url = _currentTab.Url,
                    Title = _currentTab.Title,
                    StartUtc = now,
                    LastSeenUtc = now
                };
                _logger.LogInformation(
                    "WEB START: {browser} {domain} {url} @ {start}",
                    _active.Browser,
                    _active.Domain,
                    _active.Url,
                    now);

                return sessionToClose;
            }

            _active.LastSeenUtc = now;
            if (!string.IsNullOrWhiteSpace(_currentTab.Title))
            {
                _active.Title = _currentTab.Title;
            }
            if (!string.IsNullOrWhiteSpace(_currentTab.Url))
            {
                _active.Url = _currentTab.Url;
            }

            if (_checkpointInterval > TimeSpan.Zero &&
                now - _active.StartUtc >= _checkpointInterval)
            {
                var end = now < _active.StartUtc ? DateTimeOffset.UtcNow : now;
                var sessionToClose = new WebSessionRecord(
                    Guid.NewGuid(),
                    _active.DeviceId,
                    _active.Domain,
                    _active.Title,
                    _active.Url,
                    _active.StartUtc,
                    end);

                _active = new ActiveWebSession
                {
                    DeviceId = _active.DeviceId,
                    Browser = _currentTab.Browser,
                    Domain = _currentTab.Domain,
                    Url = _currentTab.Url,
                    Title = _currentTab.Title,
                    StartUtc = end,
                    LastSeenUtc = end
                };

                return sessionToClose;
            }

            return null;
        }

        if (_active is null)
        {
            return null;
        }

        var inactiveEnd = GetInactiveEndAt(now);
        if (inactiveEnd < _active.StartUtc)
        {
            inactiveEnd = now;
        }
        var inactiveDuration = inactiveEnd - _active.StartUtc;
        if (inactiveDuration >= MinSessionDuration)
        {
            _logger.LogInformation(
                "WEB END: {browser} {domain} {start} -> {end} (secs={secs:n0})",
                _active.Browser,
                _active.Domain,
                _active.StartUtc,
                inactiveEnd,
                inactiveDuration.TotalSeconds);
        }

        var toClose = new WebSessionRecord(
            Guid.NewGuid(),
            _active.DeviceId,
            _active.Domain,
            _active.Title,
            _active.Url,
            _active.StartUtc,
            inactiveEnd);

        _active = null;
        return toClose;
    }

    private bool IsBrowserActive(DateTimeOffset now)
    {
        if (_isBrowserForeground)
        {
            return true;
        }

        if (!_lastBrowserActiveAt.HasValue)
        {
            return false;
        }

        return now - _lastBrowserActiveAt.Value <= _foregroundGrace;
    }

    private bool IsUserActive(DateTimeOffset now)
    {
        if (!_lastUserActiveAt.HasValue)
        {
            return false;
        }

        return now - _lastUserActiveAt.Value <= _idleGrace;
    }

    private static DateTimeOffset MinTimestamp(DateTimeOffset a, DateTimeOffset b)
    {
        return a <= b ? a : b;
    }

    private DateTimeOffset GetInactiveEndAt(DateTimeOffset now)
    {
        var endAt = now;
        if (_lastBrowserActiveAt.HasValue)
        {
            endAt = MinTimestamp(endAt, _lastBrowserActiveAt.Value);
        }
        if (_lastUserActiveAt.HasValue)
        {
            endAt = MinTimestamp(endAt, _lastUserActiveAt.Value);
        }

        return endAt;
    }

    private static bool IsSamePage(ActiveWebSession active, CurrentTab tab)
    {
        var urlSame = string.Equals(active.Url ?? "", tab.Url ?? "", StringComparison.Ordinal);
        var domainSame = string.Equals(active.Domain, tab.Domain, StringComparison.Ordinal);
        var browserSame = string.Equals(
            active.Browser,
            tab.Browser,
            StringComparison.OrdinalIgnoreCase);

        return browserSame && domainSame && urlSame;
    }

    private WebSessionRecord? CloseActiveLocked(DateTimeOffset end)
    {
        if (_active is null)
        {
            return null;
        }

        var safeEnd = end < _active.StartUtc ? DateTimeOffset.UtcNow : end;
        var duration = safeEnd - _active.StartUtc;
        if (duration >= MinSessionDuration)
        {
            _logger.LogInformation(
                "WEB END: {browser} {domain} {start} -> {end} (secs={secs:n0})",
                _active.Browser,
                _active.Domain,
                _active.StartUtc,
                safeEnd,
                duration.TotalSeconds);
        }

        var record = new WebSessionRecord(
            Guid.NewGuid(),
            _active.DeviceId,
            _active.Domain,
            _active.Title,
            _active.Url,
            _active.StartUtc,
            safeEnd);

        _active = null;
        return record;
    }

    private static bool IsBrowserApp(string appName)
    {
        var name = appName.Trim();
        if (name.Length == 0) return false;

        return name.Contains("chrome", StringComparison.OrdinalIgnoreCase)
            || name.Contains("msedge", StringComparison.OrdinalIgnoreCase)
            || name.Contains("edge", StringComparison.OrdinalIgnoreCase)
            || name.Contains("firefox", StringComparison.OrdinalIgnoreCase)
            || name.Contains("brave", StringComparison.OrdinalIgnoreCase)
            || name.Contains("arc", StringComparison.OrdinalIgnoreCase)
            || name.Contains("opera", StringComparison.OrdinalIgnoreCase)
            || name.Contains("vivaldi", StringComparison.OrdinalIgnoreCase)
            || name.Contains("safari", StringComparison.OrdinalIgnoreCase);
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

    private sealed record CurrentTab(
        string Domain,
        string? Url,
        string? Title,
        string Browser,
        DateTimeOffset LastEventAtUtc);

    private sealed class ActiveWebSession
    {
        public required string DeviceId { get; init; }
        public required string Browser { get; init; }
        public required string Domain { get; init; }
        public string? Url { get; set; }
        public string? Title { get; set; }
        public required DateTimeOffset StartUtc { get; init; }
        public required DateTimeOffset LastSeenUtc { get; set; }
    }
}

public sealed record WebSessionRecord(
    Guid SessionId,
    string DeviceId,
    string Domain,
    string? Title,
    string? Url,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc);
