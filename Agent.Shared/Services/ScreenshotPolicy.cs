namespace Agent.Shared.Services;

public static class ScreenshotPolicy
{
    public const string TriggerAppSwitch = "app_switch";
    public const string TriggerSplitScreen = "split_screen";
    public const string TriggerInterval = "interval";

    public static bool ShouldCapture(
        bool enabled,
        DateTimeOffset? lastCaptureUtc,
        DateTimeOffset nowUtc,
        int intervalMinutes,
        string triggerReason)
    {
        if (!enabled)
        {
            return false;
        }

        if (intervalMinutes <= 0)
        {
            intervalMinutes = 5;
        }

        var forceTrigger = string.Equals(triggerReason, TriggerAppSwitch, StringComparison.OrdinalIgnoreCase)
            || string.Equals(triggerReason, TriggerSplitScreen, StringComparison.OrdinalIgnoreCase);

        if (forceTrigger)
        {
            return true;
        }

        if (!lastCaptureUtc.HasValue)
        {
            return true;
        }

        return nowUtc - lastCaptureUtc.Value >= TimeSpan.FromMinutes(intervalMinutes);
    }
}
