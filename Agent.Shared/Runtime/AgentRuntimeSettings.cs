namespace Agent.Shared.Runtime;

public sealed record AgentRuntimeSettings(
    bool EnableScreenshots = false,
    int ScreenshotIntervalMinutes = 5,
    int ScreenshotRetentionDays = 14);
