using Agent.Shared.Config;
using Agent.Shared.LocalApi;
using Agent.Shared.Models;
using Agent.Shared.Runtime;
using Agent.Windows.Native;
using Agent.Windows.Processing;
using Agent.Windows.Services;

internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitMissingToken = 2;
    private const int ExitUnauthorized = 3;
    private const int ExitUnreachable = 4;
    private const int ExitContractMismatch = 5;
    private const int ExitRepeatedFailures = 6;

    private const string LocalApiUrlEnv = "AGENT_LOCAL_API_URL";
    private const string LocalApiTokenEnv = "AGENT_LOCAL_API_TOKEN";
    private const string PollSecondsEnv = "AGENT_POLL_SECONDS";
    private const string FailureExitSecondsEnv = "AGENT_FAILURE_EXIT_SECONDS";

    private static async Task<int> Main(string[] args)
    {
        var cliUrl = GetArgValue(args, "--url");
        var cliToken = GetArgValue(args, "--token");
        var configPaths = GetConfigPaths().ToArray();

        var apiBaseUrl = LocalApiRuntimeResolver.ResolveBaseUrl(
            cliUrl,
            LocalApiUrlEnv,
            configPaths,
            LocalApiConstants.DefaultBaseUrl);

        var (token, tokenSource) = LocalApiRuntimeResolver.ResolveToken(
            cliToken,
            LocalApiTokenEnv,
            configPaths,
            new AgentConfig().GlobalLocalApiToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("AGENT_LOCAL_API_TOKEN is required.");
            return ExitMissingToken;
        }

        var pollSeconds = ReadIntEnv(PollSecondsEnv, 2);
        var failureExitSeconds = ReadIntEnv(FailureExitSecondsEnv, 60);
        var runtimeSettings = AgentRuntimeSettingsLoader.Load(configPaths);

        LogStartupSummary(apiBaseUrl, pollSeconds, failureExitSeconds, tokenSource);

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var apiClient = new LocalApiClient(httpClient, apiBaseUrl, token);
        var processor = new WindowsActivityProcessor(
            apiClient,
            new WindowsScreenshotService(),
            LogPostFailure,
            message => Console.Error.WriteLine(message));

        using var cts = new CancellationTokenSource();
        void RequestStop()
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            RequestStop();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RequestStop();

        Console.WriteLine("Preflight: /health");
        var health = await apiClient.GetHealthAsync(cts.Token);
        if (!health.Success)
        {
            return HandlePreflightFailure("health", health.IsUnauthorized, health.Error);
        }

        Console.WriteLine("Preflight: /version");
        var version = await apiClient.GetVersionAsync(cts.Token);
        if (!version.Success)
        {
            return HandlePreflightFailure("version", version.IsUnauthorized, version.Error);
        }

        if (!string.Equals(version.Value?.Contract, LocalApiConstants.Contract, StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                $"Local API contract mismatch. expected={LocalApiConstants.Contract} got={version.Value?.Contract ?? "unknown"}");
            return ExitContractMismatch;
        }

        LogConnected(version.Value);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(pollSeconds));
        var lastSuccessAt = DateTimeOffset.UtcNow;
        var failureWindow = TimeSpan.FromSeconds(failureExitSeconds);
        var unreachableSinceLastSuccess = false;
        var lastForegroundSignature = string.Empty;
        DateTimeOffset? lastScreenshotAt = null;

        try
        {
            while (await timer.WaitForNextTickAsync(cts.Token))
            {
                var now = DateTimeOffset.UtcNow;
                var hadSuccess = false;
                var hadUnreachable = false;
                var currentForegroundSignature = lastForegroundSignature;

                var idleResult = await ProcessIdleSampleAsync(apiClient, now, cts.Token);
                hadSuccess |= idleResult.HadSuccess;
                hadUnreachable |= idleResult.HadUnreachable;
                if (idleResult.Unauthorized)
                {
                    return ExitUnauthorized;
                }

                var appResult = await ProcessForegroundSampleAsync(apiClient, now, cts.Token);
                hadSuccess |= appResult.HadSuccess;
                hadUnreachable |= appResult.HadUnreachable;
                if (appResult.Unauthorized)
                {
                    return ExitUnauthorized;
                }

                if (!string.IsNullOrWhiteSpace(appResult.ForegroundSignature))
                {
                    currentForegroundSignature = appResult.ForegroundSignature;
                }

                var activityResult = await processor.ProcessAsync(
                    now,
                    runtimeSettings,
                    currentForegroundSignature,
                    lastScreenshotAt,
                    cts.Token);

                hadSuccess |= activityResult.HadSuccess;
                hadUnreachable |= activityResult.HadUnreachable;
                if (activityResult.Unauthorized)
                {
                    return ExitUnauthorized;
                }

                lastForegroundSignature = activityResult.ForegroundSignature;
                lastScreenshotAt = activityResult.LastScreenshotAt;

                if (hadSuccess)
                {
                    lastSuccessAt = now;
                    unreachableSinceLastSuccess = false;
                }
                else
                {
                    if (hadUnreachable)
                    {
                        unreachableSinceLastSuccess = true;
                    }

                    if (now - lastSuccessAt > failureWindow)
                    {
                        var exitCode = unreachableSinceLastSuccess ? ExitUnreachable : ExitRepeatedFailures;
                        Console.Error.WriteLine(
                            $"No successful POSTs for {failureWindow.TotalSeconds:0}s. Exiting with code {exitCode}.");
                        return exitCode;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }

        Console.WriteLine("Agent.Windows stopping.");
        return ExitOk;
    }

    private static async Task<(bool HadSuccess, bool HadUnreachable, bool Unauthorized)> ProcessIdleSampleAsync(
        LocalApiClient apiClient,
        DateTimeOffset now,
        CancellationToken ct)
    {
        try
        {
            var idleSeconds = WindowsInput.GetIdleSeconds();
            var result = await apiClient.PostIdleAsync(new IdleSampleRequest(idleSeconds, now), ct);
            if (result.Success)
            {
                return (true, false, false);
            }

            LogPostFailure("idle", result);
            return (false, result.StatusCode is null, result.IsUnauthorized);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Idle sample failed: {ex.Message}");
            return (false, false, false);
        }
    }

    private static async Task<(bool HadSuccess, bool HadUnreachable, bool Unauthorized, string? ForegroundSignature)> ProcessForegroundSampleAsync(
        LocalApiClient apiClient,
        DateTimeOffset now,
        CancellationToken ct)
    {
        try
        {
            var fg = WindowsInput.GetForegroundApp();
            if (fg is null || string.IsNullOrWhiteSpace(fg.AppName))
            {
                return (false, false, false, null);
            }

            var appName = TruncateRequired(fg.AppName, 128);
            var windowTitle = TruncateOptional(fg.WindowTitle, 256);

            var result = await apiClient.PostAppFocusAsync(new AppFocusRequest(appName, windowTitle, now), ct);
            if (result.Success)
            {
                return (true, false, false, $"{appName}|{windowTitle}");
            }

            LogPostFailure("app-focus", result);
            return (false, result.StatusCode is null, result.IsUnauthorized, null);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"App focus sample failed: {ex.Message}");
            return (false, false, false, null);
        }
    }

    private static int HandlePreflightFailure(string step, bool isUnauthorized, string? error)
    {
        if (isUnauthorized)
        {
            Console.Error.WriteLine($"Local API {step} unauthorized. Verify {LocalApiConstants.AuthHeaderName} token.");
            return ExitUnauthorized;
        }

        Console.Error.WriteLine($"Local API {step} failed: {error ?? "unreachable"}");
        return ExitUnreachable;
    }

    private static void LogStartupSummary(string localApiUrl, int pollSeconds, int failureExitSeconds, string tokenSource)
    {
        Console.WriteLine(
            """
            Agent starting
            ------------------------
            OS: Windows
            LocalApiUrl: {0}
            LocalApiToken: SET
            LocalApiTokenSource: {1}
            PollIntervalSeconds: {2}
            FailureExitSeconds: {3}
            ------------------------
            """,
            localApiUrl,
            tokenSource,
            pollSeconds,
            failureExitSeconds);
    }

    private static void LogConnected(LocalVersionResponse? version)
    {
        Console.WriteLine(
            """
            Connected to Local API
            ------------------------
            contract: {0}
            deviceId: {1}
            agentVersion: {2}
            ------------------------
            """,
            version?.Contract ?? "unknown",
            version?.DeviceId ?? "unknown",
            version?.AgentVersion ?? "unknown");
    }

    private static void LogPostFailure(string kind, LocalApiResult result)
    {
        var status = result.StatusCode is null ? "n/a" : $"{(int)result.StatusCode} {result.StatusCode}";
        var detail = string.IsNullOrWhiteSpace(result.Error) ? "unknown error" : result.Error;
        Console.Error.WriteLine($"POST /events/{kind} failed: {status} {detail}");
    }

    private static string TruncateRequired(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? TruncateOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static int ReadIntEnv(string name, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out var parsed) && parsed > 0 ? parsed : defaultValue;
    }

    private static IEnumerable<string> GetConfigPaths()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(programData))
        {
            yield return Path.Combine(programData, "EmployeeTracker", "appsettings.json");
        }

        yield return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    private static string? GetArgValue(string[] args, string name)
    {
        if (args.Length == 0) return null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    return args[i + 1];
                }

                return null;
            }

            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[(name.Length + 1)..];
            }
        }

        return null;
    }
}
