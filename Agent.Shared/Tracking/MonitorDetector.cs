using Agent.Shared.Models;

namespace Agent.Shared.Tracking;

public static class MonitorDetector
{
    public static IReadOnlyList<ActivitySample> BuildMonitorSamples(
        IReadOnlyList<MonitorInfoDto> monitors,
        IReadOnlyList<VisibleWindowDto> windows,
        bool pipActive,
        bool videoPlaying,
        DateTimeOffset timestampUtc)
    {
        var monitorById = monitors.ToDictionary(m => m.MonitorId, StringComparer.OrdinalIgnoreCase);
        var splitScreen = SplitScreenDetector.IsSplitScreen(windows, monitorById);
        var splitScreenAttention = SplitScreenDetector.IsSplitScreenForAttention(windows, monitorById);

        // Lowest z-order number is treated as the topmost visible window.
        var activeWindowByMonitor = windows
            .Where(w => w.IsVisible && !w.IsMinimized)
            .GroupBy(w => w.MonitorId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(w => w.ZOrder).First(),
                StringComparer.OrdinalIgnoreCase);

        var result = new List<ActivitySample>(monitors.Count);
        foreach (var monitor in monitors)
        {
            if (!activeWindowByMonitor.TryGetValue(monitor.MonitorId, out var active))
            {
                continue;
            }

            var browserForeground = IsBrowser(active.ProcessName);
            var attention = AttentionScoreCalculator.Compute(
                pipActive,
                browserForeground,
                splitScreenAttention,
                videoPlaying);

            result.Add(new ActivitySample(
                active.ProcessName,
                active.WindowTitle,
                monitor.MonitorId,
                active.WindowBounds,
                splitScreen,
                pipActive,
                videoPlaying,
                monitors.Count,
                attention,
                timestampUtc));
        }

        return result;
    }

    private static bool IsBrowser(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        return processName.Contains("chrome", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("msedge", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("edge", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("firefox", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("brave", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("opera", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("safari", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("arc", StringComparison.OrdinalIgnoreCase);
    }
}
