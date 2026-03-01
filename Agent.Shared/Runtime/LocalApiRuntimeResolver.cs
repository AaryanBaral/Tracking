using System.Text.Json;

namespace Agent.Shared.Runtime;

public static class LocalApiRuntimeResolver
{
    public static (string Token, string Source) ResolveToken(
        string? cliToken,
        string tokenEnvironmentVariable,
        IEnumerable<string> configPaths,
        string fallbackToken = "")
    {
        if (!string.IsNullOrWhiteSpace(cliToken))
        {
            return (cliToken, "cli");
        }

        var envToken = Environment.GetEnvironmentVariable(tokenEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            return (envToken, tokenEnvironmentVariable);
        }

        var configToken = ReadString(configPaths, "LocalApi", "Token");
        if (!string.IsNullOrWhiteSpace(configToken))
        {
            return (configToken, "appsettings.json");
        }

        if (!string.IsNullOrWhiteSpace(fallbackToken))
        {
            return (fallbackToken, "fallback");
        }

        return (string.Empty, "missing");
    }

    public static string ResolveBaseUrl(
        string? cliUrl,
        string urlEnvironmentVariable,
        IEnumerable<string> configPaths,
        string defaultBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(cliUrl))
        {
            return cliUrl;
        }

        var envUrl = Environment.GetEnvironmentVariable(urlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            return envUrl;
        }

        foreach (var path in configPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("Agent", out var agent)
                    && agent.TryGetProperty("LocalApiPort", out var portProp)
                    && portProp.TryGetInt32(out var port)
                    && port > 0)
                {
                    return $"http://127.0.0.1:{port}";
                }
            }
            catch
            {
                // ignore invalid config files
            }
        }

        return defaultBaseUrl;
    }

    private static string? ReadString(IEnumerable<string> configPaths, string section, string key)
    {
        foreach (var path in configPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);
                if (!doc.RootElement.TryGetProperty(section, out var sectionNode))
                {
                    continue;
                }

                if (sectionNode.TryGetProperty(key, out var keyNode))
                {
                    var value = keyNode.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
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
}
