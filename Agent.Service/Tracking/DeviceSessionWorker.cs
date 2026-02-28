using System.Text.Json;
using Agent.Service.Infrastructure;
using Agent.Service.Identity;
using Agent.Shared.Config;
using Microsoft.Extensions.Options;

namespace Agent.Service.Tracking;

public sealed class DeviceSessionWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly SemaphoreSlim IoLock = new(1, 1);
    private readonly IOutboxService _outbox;
    private readonly DeviceIdentityStore _identityStore;
    private readonly ILogger<DeviceSessionWorker> _logger;
    private readonly TimeSpan _checkpointInterval;
    private DeviceSessionState? _state;

    public DeviceSessionWorker(
        IOutboxService outbox,
        DeviceIdentityStore identityStore,
        ILogger<DeviceSessionWorker> logger,
        IOptions<AgentConfig> options)
    {
        _outbox = outbox;
        _identityStore = identityStore;
        _logger = logger;
        var checkpointSeconds = Math.Max(0, options.Value.DeviceSessionCheckpointSeconds);
        _checkpointInterval = checkpointSeconds > 0
            ? TimeSpan.FromSeconds(checkpointSeconds)
            : TimeSpan.Zero;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitializeAsync(stoppingToken);
        if (_checkpointInterval == TimeSpan.Zero)
        {
            return;
        }

        await RunInitialCheckpointAsync(stoppingToken);

        using var timer = new PeriodicTimer(_checkpointInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckpointAsync(stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await CloseSessionAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    private async Task InitializeAsync(CancellationToken ct)
    {
        await IoLock.WaitAsync(ct);
        try
        {
            var identity = await _identityStore.GetOrCreateAsync(ct);
            var deviceId = identity.DeviceId.ToString();
            var now = DateTimeOffset.UtcNow;

            var existing = await TryReadStateAsync(ct);
            if (existing is not null && string.Equals(existing.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                await EnqueueIfValidAsync(new DeviceSessionRecord(
                    existing.SessionId,
                    existing.DeviceId,
                    existing.StartAtUtc,
                    now));
            }

            _state = new DeviceSessionState(Guid.NewGuid(), deviceId, now);
            await WriteStateAsync(_state, ct);
            _logger.LogInformation("DEVICE START @ {start}", now);
        }
        finally
        {
            IoLock.Release();
        }
    }

    private async Task CheckpointAsync(CancellationToken ct)
    {
        await IoLock.WaitAsync(ct);
        try
        {
            _state ??= await TryReadStateAsync(ct);
            if (_state is null)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var record = new DeviceSessionRecord(
                _state.SessionId,
                _state.DeviceId,
                _state.StartAtUtc,
                now);
            await EnqueueIfValidAsync(record);

            _state = new DeviceSessionState(Guid.NewGuid(), _state.DeviceId, now);
            await WriteStateAsync(_state, ct);
        }
        finally
        {
            IoLock.Release();
        }
    }

    private async Task RunInitialCheckpointAsync(CancellationToken ct)
    {
        var initialDelay = TimeSpan.FromSeconds(1);
        if (_checkpointInterval <= initialDelay)
        {
            return;
        }

        try
        {
            await Task.Delay(initialDelay, ct);
            await CheckpointAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested before initial checkpoint.
        }
    }

    private async Task CloseSessionAsync(CancellationToken ct)
    {
        await IoLock.WaitAsync(ct);
        try
        {
            _state ??= await TryReadStateAsync(ct);
            if (_state is null)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            await EnqueueIfValidAsync(new DeviceSessionRecord(
                _state.SessionId,
                _state.DeviceId,
                _state.StartAtUtc,
                now));

            DeleteStateFile();
            _state = null;
        }
        finally
        {
            IoLock.Release();
        }
    }

    private async Task EnqueueIfValidAsync(DeviceSessionRecord record)
    {
        if (record.EndAtUtc <= record.StartAtUtc)
        {
            return;
        }

        await _outbox.EnqueueAsync("device_session", record);
        _logger.LogInformation(
            "DEVICE ON: {start} -> {end} (secs={secs:n0})",
            record.StartAtUtc,
            record.EndAtUtc,
            (record.EndAtUtc - record.StartAtUtc).TotalSeconds);
    }

    private static async Task<DeviceSessionState?> TryReadStateAsync(CancellationToken ct)
    {
        var path = GetStatePath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<DeviceSessionState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteStateAsync(DeviceSessionState state, CancellationToken ct)
    {
        var path = GetStatePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    private static void DeleteStateFile()
    {
        var path = GetStatePath();
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private static string GetStatePath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(baseDir, "EmployeeTracker", "device-session.json");
    }
}

public sealed record DeviceSessionRecord(
    Guid SessionId,
    string DeviceId,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc);

internal sealed record DeviceSessionState(
    Guid SessionId,
    string DeviceId,
    DateTimeOffset StartAtUtc);
