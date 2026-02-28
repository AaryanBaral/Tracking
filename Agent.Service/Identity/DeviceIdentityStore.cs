using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32;

namespace Agent.Service.Identity;

public sealed class DeviceIdentityStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
    private static readonly SemaphoreSlim IoLock = new(1, 1);

    public async Task<DeviceIdentity> GetOrCreateAsync(CancellationToken ct)
    {
        await IoLock.WaitAsync(ct);
        try
        {
            var path = GetPath();
            if (OperatingSystem.IsWindows())
            {
                var machineGuid = TryGetWindowsMachineGuid();
                if (machineGuid is not null)
                {
                    var existing = await TryReadAsync(path, ct);
                    if (existing is null || existing.DeviceId != machineGuid.Value)
                    {
                        var identity = existing ?? new DeviceIdentity();
                        identity.DeviceId = machineGuid.Value;
                        identity.CreatedAtUtc = existing?.CreatedAtUtc ?? DateTimeOffset.UtcNow;
                        identity.Hostname = existing?.Hostname ?? Environment.MachineName;
                        identity.Os = existing?.Os ?? GetOsName();
                        identity.AgentVersion = existing?.AgentVersion ?? GetAgentVersion();

                        await WriteAsync(path, identity, ct);
                        return identity;
                    }

                    return existing;
                }
            }

            var loaded = await TryReadAsync(path, ct);
            if (loaded is not null && loaded.DeviceId != Guid.Empty)
            {
                if (string.IsNullOrWhiteSpace(loaded.AgentVersion))
                {
                    loaded.AgentVersion = GetAgentVersion();
                    await WriteAsync(path, loaded, ct);
                }
                return loaded;
            }

            var created = new DeviceIdentity
            {
                DeviceId = Guid.NewGuid(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Hostname = Environment.MachineName,
                Os = GetOsName(),
                AgentVersion = GetAgentVersion()
            };

            await WriteAsync(path, created, ct);

            return created;
        }
        finally
        {
            IoLock.Release();
        }
    }

    private static string GetPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(baseDir, "EmployeeTracker", "device.json");
    }

    private static async Task<DeviceIdentity?> TryReadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<DeviceIdentity>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteAsync(string path, DeviceIdentity identity, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var payload = JsonSerializer.Serialize(identity, JsonOptions);
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var stream = new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(payload.AsMemory(), ct);
                await writer.FlushAsync();
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(50 * attempt, ct);
            }
        }
    }

    private static string GetAgentVersion()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
    }

    private static string GetOsName()
    {
        return RuntimeInformation.OSDescription;
    }

    [SupportedOSPlatform("windows")]
    private static Guid? TryGetWindowsMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var raw = key?.GetValue("MachineGuid") as string;
            return Guid.TryParse(raw, out var g) ? g : null;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class DeviceIdentity
{
    public Guid DeviceId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? Hostname { get; set; }
    public string? Os { get; set; }
    public string? AgentVersion { get; set; }
}
