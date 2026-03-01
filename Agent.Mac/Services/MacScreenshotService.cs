using System.Runtime.InteropServices;
using Agent.Shared.Abstractions;
using Agent.Shared.Models;

namespace Agent.Mac.Services;

public sealed class MacScreenshotService : IScreenshotService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyList<ScreenshotCaptureResult>> CaptureAsync(
        ScreenshotCaptureRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<ScreenshotCaptureResult>();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var monitor in request.Monitors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var filePath = BuildFilePath(monitor.MonitorId, request.TimestampUtc);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                // Validate that CoreGraphics can resolve the target display.
                if (!uint.TryParse(monitor.MonitorId, out var displayId))
                {
                    continue;
                }

                var image = CGDisplayCreateImage(displayId);
                if (image != IntPtr.Zero)
                {
                    CGImageRelease(image);
                }

                // Use native screencapture to encode JPG on disk without loading large pixel buffers in managed memory.
                await Shell.RunAsync(
                    "/usr/sbin/screencapture",
                    new[] { "-x", "-D", displayId.ToString(), filePath },
                    cancellationToken);

                results.Add(new ScreenshotCaptureResult(
                    monitor.MonitorId,
                    filePath,
                    request.TriggerReason,
                    request.TimestampUtc));
            }
        }
        finally
        {
            _gate.Release();
        }

        return results;
    }

    private static string BuildFilePath(string monitorId, DateTimeOffset timestampUtc)
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EmployeeTracker",
            "screenshots",
            timestampUtc.ToString("yyyyMMdd"));

        var safeMonitorId = string.Concat(monitorId.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        var filename = $"{timestampUtc:yyyyMMdd_HHmmss}_{safeMonitorId}.jpg";
        return Path.Combine(baseDir, filename);
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGDisplayCreateImage(uint displayId);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGImageRelease(IntPtr image);
}
