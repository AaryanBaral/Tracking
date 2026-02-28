using Agent.Service.Infrastructure;
using Agent.Service.Identity;
using Agent.Shared.Config;
using Microsoft.Extensions.Options;

namespace Agent.Service.Tracking;

public sealed record IdleSessionRecord(
    Guid SessionId,
    string DeviceId,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc
);

public sealed class IdleSessionizer
{
    private readonly IOutboxService _outbox;
    private readonly DeviceIdentityStore _identityStore;
    private readonly ILogger<IdleSessionizer> _logger;

    private bool _isIdle;
    private DateTimeOffset? _idleStartUtc;
    private DateTimeOffset? _lastCheckpointUtc;
    private readonly double _thresholdSeconds;
    private readonly TimeSpan _checkpointInterval;

    public IdleSessionizer(
        IOutboxService outbox, 
        DeviceIdentityStore identityStore,
        ILogger<IdleSessionizer> logger,
        IOptions<AgentConfig> options)
    {
        _outbox = outbox;
        _identityStore = identityStore;
        _logger = logger;
        var threshold = options.Value.IdleThresholdSeconds;
        _thresholdSeconds = threshold > 0 ? threshold : 60.0;
        var checkpointSeconds = Math.Max(0, options.Value.IdleSessionCheckpointSeconds);
        _checkpointInterval = checkpointSeconds > 0
            ? TimeSpan.FromSeconds(checkpointSeconds)
            : TimeSpan.Zero;
    }

    public async Task HandleIdleStateAsync(TimeSpan idleDuration, DateTimeOffset timestamp)
    {
        var isIdleNow = idleDuration.TotalSeconds >= _thresholdSeconds;

        if (!_isIdle && isIdleNow)
        {
            // Transition: Active -> Idle
            // IMPORTANT: Start time is effectively in the past
            // We subtract the accumulated duration to find when it started
            var threshold = TimeSpan.FromSeconds(_thresholdSeconds);
            var adjustedStart = timestamp - idleDuration + threshold;
            _idleStartUtc = adjustedStart > timestamp ? timestamp : adjustedStart;
            _lastCheckpointUtc = _idleStartUtc;
            _isIdle = true;
            _logger.LogInformation("IDLE START @ {start}", _idleStartUtc);
        }
        else if (_isIdle && !isIdleNow)
        {
            // Transition: Idle -> Active
            // End time is roughly now (or timestamp)
            var start = _lastCheckpointUtc ?? _idleStartUtc ?? timestamp.AddSeconds(-_thresholdSeconds);
            var end = timestamp;
            
            _isIdle = false;
            _idleStartUtc = null;
            _lastCheckpointUtc = null;

            var identity = await _identityStore.GetOrCreateAsync(CancellationToken.None);
            var record = new IdleSessionRecord(
                Guid.NewGuid(),
                identity.DeviceId.ToString(),
                start,
                end
            );

            await _outbox.EnqueueAsync("idle_session", record);
            _logger.LogInformation(
                "IDLE END {start} -> {end} (secs={secs:n0})",
                record.StartAtUtc,
                record.EndAtUtc,
                (record.EndAtUtc - record.StartAtUtc).TotalSeconds);
        }
        else if (_isIdle && isIdleNow)
        {
            if (_checkpointInterval > TimeSpan.Zero && _lastCheckpointUtc.HasValue)
            {
                var elapsed = timestamp - _lastCheckpointUtc.Value;
                if (elapsed >= _checkpointInterval)
                {
                    var start = _lastCheckpointUtc.Value;
                    var end = timestamp;
                    _lastCheckpointUtc = timestamp;

                    var identity = await _identityStore.GetOrCreateAsync(CancellationToken.None);
                    var record = new IdleSessionRecord(
                        Guid.NewGuid(),
                        identity.DeviceId.ToString(),
                        start,
                        end
                    );

                    await _outbox.EnqueueAsync("idle_session", record);
                    _logger.LogInformation(
                        "IDLE CHECKPOINT {start} -> {end} (secs={secs:n0})",
                        record.StartAtUtc,
                        record.EndAtUtc,
                        (record.EndAtUtc - record.StartAtUtc).TotalSeconds);
                }
            }
        }
    }

    public async Task CloseActiveAsync()
    {
        if (_isIdle && _idleStartUtc.HasValue)
        {
            var end = DateTimeOffset.UtcNow;
            var identity = await _identityStore.GetOrCreateAsync(CancellationToken.None);
            
            var record = new IdleSessionRecord(
                Guid.NewGuid(),
                identity.DeviceId.ToString(),
                _lastCheckpointUtc ?? _idleStartUtc.Value,
                end
            );
            
            await _outbox.EnqueueAsync("idle_session", record);
            _logger.LogInformation(
                "IDLE END {start} -> {end} (secs={secs:n0})",
                record.StartAtUtc,
                record.EndAtUtc,
                (record.EndAtUtc - record.StartAtUtc).TotalSeconds);
            _isIdle = false;
            _idleStartUtc = null;
            _lastCheckpointUtc = null;
        }
    }
}
