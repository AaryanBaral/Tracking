using System.Drawing;
using System.Drawing.Imaging;
using Agent.Shared.Abstractions;
using Agent.Shared.Models;

namespace Agent.Windows.Services;

public sealed class WindowsScreenshotService : IScreenshotService
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

                using var bitmap = new Bitmap(monitor.Width, monitor.Height);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(
                        monitor.X,
                        monitor.Y,
                        0,
                        0,
                        new Size(monitor.Width, monitor.Height),
                        CopyPixelOperation.SourceCopy);
                }

                SaveJpeg(bitmap, filePath, quality: 70L);
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
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EmployeeTracker",
            "screenshots",
            timestampUtc.ToString("yyyyMMdd"));

        var safeMonitorId = string.Concat(monitorId.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        var filename = $"{timestampUtc:yyyyMMdd_HHmmss}_{safeMonitorId}.jpg";
        return Path.Combine(baseDir, filename);
    }

    private static void SaveJpeg(Image image, string path, long quality)
    {
        var encoder = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(codec => codec.FormatID == ImageFormat.Jpeg.Guid);

        if (encoder is null)
        {
            image.Save(path, ImageFormat.Jpeg);
            return;
        }

        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        image.Save(path, encoder, parameters);
    }
}
