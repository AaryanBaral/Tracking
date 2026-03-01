using Agent.Shared.Models;

namespace Agent.Shared.Abstractions;

public interface IScreenshotService
{
    Task<IReadOnlyList<ScreenshotCaptureResult>> CaptureAsync(
        ScreenshotCaptureRequest request,
        CancellationToken cancellationToken);
}
