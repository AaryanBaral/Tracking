using Agent.Shared.Models;

namespace Agent.Shared.Tracking;

public static class SplitScreenDetector
{
    // Window must cover this much of a monitor to be considered "large" for split view detection.
    public const double LargeWindowThreshold = 0.30;

    // Attention-scoring threshold requested by product rules.
    public const double AttentionPenaltyThreshold = 0.40;

    public static bool IsSplitScreen(
        IReadOnlyList<VisibleWindowDto> windows,
        IReadOnlyDictionary<string, MonitorInfoDto> monitorById)
    {
        return CountLargeWindowsByMonitor(windows, monitorById, LargeWindowThreshold)
            .Any(pair => pair.Value >= 2);
    }

    public static bool IsSplitScreenForAttention(
        IReadOnlyList<VisibleWindowDto> windows,
        IReadOnlyDictionary<string, MonitorInfoDto> monitorById)
    {
        return CountLargeWindowsByMonitor(windows, monitorById, AttentionPenaltyThreshold)
            .Any(pair => pair.Value >= 2);
    }

    private static Dictionary<string, int> CountLargeWindowsByMonitor(
        IReadOnlyList<VisibleWindowDto> windows,
        IReadOnlyDictionary<string, MonitorInfoDto> monitorById,
        double threshold)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var window in windows)
        {
            if (!window.IsVisible || window.IsMinimized)
            {
                continue;
            }

            if (!monitorById.TryGetValue(window.MonitorId, out var monitor))
            {
                continue;
            }

            var monitorArea = Math.Max(1, monitor.Width * monitor.Height);
            var windowArea = Math.Max(0, window.WindowBounds.Width * window.WindowBounds.Height);
            var coverage = (double)windowArea / monitorArea;
            if (coverage < threshold)
            {
                continue;
            }

            counts.TryGetValue(window.MonitorId, out var existing);
            counts[window.MonitorId] = existing + 1;
        }

        return counts;
    }
}
