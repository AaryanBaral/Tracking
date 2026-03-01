using System.Text.Json;

namespace Agent.Shared.Runtime;

public static class AgentRuntimeSettingsLoader
{
    public static AgentRuntimeSettings Load(IEnumerable<string> configPaths)
    {
        var settings = new AgentRuntimeSettings();
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
                if (!doc.RootElement.TryGetProperty("Agent", out var agent))
                {
                    continue;
                }

                if (agent.TryGetProperty("EnableScreenshots", out var screenshotsEnabled))
                {
                    if (screenshotsEnabled.ValueKind == JsonValueKind.True)
                    {
                        settings = settings with { EnableScreenshots = true };
                    }
                    else if (screenshotsEnabled.ValueKind == JsonValueKind.False)
                    {
                        settings = settings with { EnableScreenshots = false };
                    }
                }

                if (agent.TryGetProperty("ScreenshotIntervalMinutes", out var intervalProp)
                    && intervalProp.TryGetInt32(out var interval)
                    && interval > 0)
                {
                    settings = settings with { ScreenshotIntervalMinutes = interval };
                }

                if (agent.TryGetProperty("ScreenshotRetentionDays", out var retentionProp)
                    && retentionProp.TryGetInt32(out var retention)
                    && retention > 0)
                {
                    settings = settings with { ScreenshotRetentionDays = retention };
                }
            }
            catch
            {
                // Ignore malformed config files and continue with last known good values.
            }
        }

        return settings;
    }
}
