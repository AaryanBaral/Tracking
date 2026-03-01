using System.Runtime.InteropServices;
using System.Text.Json;
using Agent.Mac.Collectors;
using Agent.Mac.Processing;
using Agent.Mac.Services;
using Agent.Shared.Config;
using Agent.Shared.LocalApi;
using Agent.Shared.Models;
using Agent.Shared.Runtime;

internal static class Program
{
    private const string LocalApiUrlEnv = "AGENT_LOCAL_API_URL";
    private const string LocalApiTokenEnv = "AGENT_LOCAL_API_TOKEN";
    private const string PollSecondsEnv = "AGENT_POLL_SECONDS";

    private static async Task Main()
    {
        if (!PreflightPermissions())
        {
            Console.Error.WriteLine("Missing macOS permissions. Grant Accessibility and Screen Recording before starting Agent.Mac.");
            return;
        }

        var configPaths = GetConfigPaths().ToArray();
        var apiBaseUrl = LocalApiRuntimeResolver.ResolveBaseUrl(
            cliUrl: null,
            urlEnvironmentVariable: LocalApiUrlEnv,
            configPaths,
            LocalApiConstants.DefaultBaseUrl);

        var (token, tokenSource) = LocalApiRuntimeResolver.ResolveToken(
            cliToken: null,
            tokenEnvironmentVariable: LocalApiTokenEnv,
            configPaths,
            fallbackToken: new AgentConfig().GlobalLocalApiToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("AGENT_LOCAL_API_TOKEN is required.");
            return;
        }

        var pollSeconds = ReadIntEnv(PollSecondsEnv, 2);
        var runtimeSettings = AgentRuntimeSettingsLoader.Load(configPaths);

        var appCollector = new MacAppCollector();
        var idleCollector = new MacIdleCollector();

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var apiClient = new LocalApiClient(httpClient, apiBaseUrl, token);
        var activityProcessor = new MacActivityProcessor(
            apiClient,
            appCollector,
            new MacScreenshotService(),
            LogPostFailure,
            message => Console.Error.WriteLine(message));

        var deviceId = TryLoadDeviceId() ?? "unknown";
        LogStartupSummary(deviceId, apiBaseUrl, pollSeconds, tokenSource);

        var health = await apiClient.GetHealthAsync(CancellationToken.None);
        if (!EnsurePreflight("health", health))
        {
            return;
        }

        var version = await apiClient.GetVersionAsync(CancellationToken.None);
        if (!EnsurePreflight("version", version))
        {
            return;
        }

        if (!string.Equals(version.Value?.Contract, LocalApiConstants.Contract, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Local API contract mismatch: {version.Value?.Contract ?? "unknown"}");
            return;
        }

        Console.WriteLine($"Local API contract: {version.Value?.Contract} DeviceId: {version.Value?.DeviceId} AgentVersion: {version.Value?.AgentVersion}");
        Console.WriteLine("Agent.Mac started.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(pollSeconds));
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

        var lastForegroundSignature = string.Empty;
        DateTimeOffset? lastScreenshotAt = null;

        try
        {
            while (await timer.WaitForNextTickAsync(cts.Token))
            {
                var now = DateTimeOffset.UtcNow;

                try
                {
                    var idle = await idleCollector.GetIdleAsync(cts.Token);
                    if (idle is not null)
                    {
                        var idleResult = await apiClient.PostIdleAsync(
                            new IdleSampleRequest(idle.IdleDuration.TotalSeconds, idle.TimestampUtc),
                            cts.Token);

                        if (!idleResult.Success)
                        {
                            LogPostFailure("idle", idleResult);
                            if (idleResult.IsUnauthorized)
                            {
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Idle sample failed: {ex.Message}");
                }

                var currentForegroundSignature = lastForegroundSignature;
                try
                {
                    var app = await appCollector.GetFocusedAppAsync(cts.Token);
                    if (app is not null && !string.IsNullOrWhiteSpace(app.AppName))
                    {
                        var appResult = await apiClient.PostAppFocusAsync(
                            new AppFocusRequest(app.AppName, app.WindowTitle, app.TimestampUtc),
                            cts.Token);

                        if (!appResult.Success)
                        {
                            LogPostFailure("app-focus", appResult);
                            if (appResult.IsUnauthorized)
                            {
                                return;
                            }
                        }
                        else
                        {
                            currentForegroundSignature = $"{app.AppName}|{app.WindowTitle}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"App focus sample failed: {ex.Message}");
                }

                var activityResult = await activityProcessor.ProcessAsync(
                    now,
                    runtimeSettings,
                    currentForegroundSignature,
                    lastScreenshotAt,
                    cts.Token);

                if (activityResult.Unauthorized)
                {
                    return;
                }

                lastForegroundSignature = activityResult.ForegroundSignature;
                lastScreenshotAt = activityResult.LastScreenshotAt;
            }
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }

        Console.WriteLine("Agent.Mac stopping.");
    }

    private static bool EnsurePreflight<T>(string step, LocalApiResult<T> result)
    {
        if (result.Success)
        {
            return true;
        }

        if (result.IsUnauthorized)
        {
            Console.Error.WriteLine($"Local API {step} check unauthorized. Verify {LocalApiConstants.AuthHeaderName}.");
            return false;
        }

        Console.Error.WriteLine($"Local API {step} check failed: {result.Error ?? "unreachable"}");
        return false;
    }

    private static void LogPostFailure(string kind, LocalApiResult result)
    {
        var status = result.StatusCode is null ? "n/a" : $"{(int)result.StatusCode} {result.StatusCode}";
        var detail = string.IsNullOrWhiteSpace(result.Error) ? "unknown error" : result.Error;
        Console.Error.WriteLine($"POST /events/{kind} failed: {status} {detail}");
    }

    private static void LogStartupSummary(string deviceId, string localApiUrl, int pollSeconds, string tokenSource)
    {
        Console.WriteLine(
            """
            Agent starting
            ------------------------
            DeviceId: {0}
            LocalApiUrl: {1}
            LocalApiToken: SET
            LocalApiTokenSource: {3}
            PollIntervalSeconds: {2}
            ------------------------
            """,
            deviceId,
            localApiUrl,
            pollSeconds,
            tokenSource);
    }

    private static string? TryLoadDeviceId()
    {
        try
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(baseDir, "EmployeeTracker", "device.json");
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("DeviceId", out var deviceId))
            {
                return deviceId.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static int ReadIntEnv(string name, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out var parsed) && parsed > 0 ? parsed : defaultValue;
    }

    private static IEnumerable<string> GetConfigPaths()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            yield return Path.Combine(appData, "EmployeeTracker", "appsettings.json");
        }

        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(commonAppData))
        {
            yield return Path.Combine(commonAppData, "EmployeeTracker", "appsettings.json");
        }

        yield return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    private static bool PreflightPermissions()
    {
        var accessibility = AXIsProcessTrusted();
        var screenCapture = CGPreflightScreenCaptureAccess();
        return accessibility && screenCapture;
    }

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern bool AXIsProcessTrusted();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern bool CGPreflightScreenCaptureAccess();
}
