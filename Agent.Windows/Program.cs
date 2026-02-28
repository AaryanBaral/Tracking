// Agent.Windows/Program.cs
// How to test:
// 1) Start Agent.Service.
// 2) Set AGENT_LOCAL_API_TOKEN (and AGENT_LOCAL_API_URL if needed).
// 3) Run Agent.Windows from a console.
// 4) Query /local/outbox/stats on the local API to confirm events.
using System.Text.Json;
using Agent.Shared.Config;
using Agent.Shared.LocalApi;
using Agent.Shared.Models;
using Agent.Windows.Native;

internal static class Program
{
    // Exit codes (keep consistent for debugging/automation)
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

        var apiBaseUrl = cliUrl
            ?? Environment.GetEnvironmentVariable(LocalApiUrlEnv)
            ?? ResolveLocalApiUrlFromConfig();
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            apiBaseUrl = LocalApiConstants.DefaultBaseUrl;
        }

        var token = ResolveToken(cliToken, out var tokenSource);

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("AGENT_LOCAL_API_TOKEN is required.");
            return ExitMissingToken;
        }

        var pollSeconds = ReadIntEnv(PollSecondsEnv, 1);
        var failureExitSeconds = ReadIntEnv(FailureExitSecondsEnv, 60);

        LogStartupSummary(apiBaseUrl, pollSeconds, failureExitSeconds, tokenSource);

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var apiClient = new LocalApiClient(httpClient, apiBaseUrl, token);

        // Graceful stop (useful for manual testing)
        using var cts = new CancellationTokenSource();
        void RequestStop()
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ProcessExit can fire after disposal.
            }
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

        // Main loop
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(pollSeconds));
        var lastSuccessAt = DateTimeOffset.UtcNow;
        var failureWindow = TimeSpan.FromSeconds(failureExitSeconds);
        var unreachableSinceLastSuccess = false;

        try
        {
            while (await timer.WaitForNextTickAsync(cts.Token))
            {
                var now = DateTimeOffset.UtcNow;
                var hadSuccess = false;
                var hadUnreachable = false;

                // 1) Idle
                try
                {
                    var idleSeconds = WindowsInput.GetIdleSeconds();
                    var idlePayload = new IdleSampleRequest(idleSeconds, now);
                    var idleResult = await apiClient.PostIdleAsync(idlePayload, cts.Token);
                    if (idleResult.Success)
                    {
                        hadSuccess = true;
                    }
                    else
                    {
                        LogPostFailure("idle", idleResult);
                        hadUnreachable |= idleResult.StatusCode is null;
                        if (idleResult.IsUnauthorized) return ExitUnauthorized;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Idle sample failed: {ex.Message}");
                }

                // 2) Foreground app + window title
                try
                {
                    var fg = WindowsInput.GetForegroundApp();
                    if (fg is not null && !string.IsNullOrWhiteSpace(fg.AppName))
                    {
                        var appName = TruncateRequired(fg.AppName, 128);
                        var windowTitle = TruncateOptional(fg.WindowTitle, 256);

                        var appPayload = new AppFocusRequest(appName, windowTitle, now);
                        var appResult = await apiClient.PostAppFocusAsync(appPayload, cts.Token);
                        if (appResult.Success)
                        {
                            hadSuccess = true;
                        }
                        else
                        {
                            LogPostFailure("app-focus", appResult);
                            hadUnreachable |= appResult.StatusCode is null;
                            if (appResult.IsUnauthorized) return ExitUnauthorized;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"App focus sample failed: {ex.Message}");
                }

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

    private static string ResolveToken(string? cliToken, out string tokenSource)
    {
        if (!string.IsNullOrWhiteSpace(cliToken))
        {
            tokenSource = "cli";
            return cliToken;
        }

        var token = Environment.GetEnvironmentVariable(LocalApiTokenEnv);
        if (!string.IsNullOrWhiteSpace(token))
        {
            tokenSource = LocalApiTokenEnv;
            return token;
        }

        token = ResolveLocalApiTokenFromConfig();
        if (!string.IsNullOrWhiteSpace(token))
        {
            tokenSource = "appsettings.json";
            return token;
        }

        token = new AgentConfig().GlobalLocalApiToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            tokenSource = "AgentConfig.GlobalLocalApiToken";
            return token;
        }

        tokenSource = "missing";
        return string.Empty;
    }

    private static string? ResolveLocalApiTokenFromConfig()
    {
        foreach (var path in GetConfigPaths())
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("LocalApi", out var localApi) &&
                    localApi.TryGetProperty("Token", out var tokenProp))
                {
                    var token = tokenProp.GetString();
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        return token;
                    }
                }
            }
            catch
            {
                // ignore invalid config files
            }
        }

        return null;
    }

    private static string? ResolveLocalApiUrlFromConfig()
    {
        foreach (var path in GetConfigPaths())
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("Agent", out var agent) &&
                    agent.TryGetProperty("LocalApiPort", out var portProp) &&
                    portProp.TryGetInt32(out var port) && port > 0)
                {
                    return $"http://127.0.0.1:{port}";
                }
            }
            catch
            {
                // ignore invalid config files
            }
        }

        return null;
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
