using Agent.Mac.Collectors;
using Agent.Mac.Services;
using Agent.Shared.LocalApi;
using Agent.Shared.Models;
using Agent.Shared.Runtime;
using Agent.Shared.Services;
using Agent.Shared.Tracking;

namespace Agent.Mac.Processing;

internal sealed class MacActivityProcessor
{
    private readonly LocalApiClient _apiClient;
    private readonly MacAppCollector _appCollector;
    private readonly MacScreenshotService _screenshotService;
    private readonly Action<string, LocalApiResult> _logPostFailure;
    private readonly Action<string> _logError;

    public MacActivityProcessor(
        LocalApiClient apiClient,
        MacAppCollector appCollector,
        MacScreenshotService screenshotService,
        Action<string, LocalApiResult> logPostFailure,
        Action<string> logError)
    {
        _apiClient = apiClient;
        _appCollector = appCollector;
        _screenshotService = screenshotService;
        _logPostFailure = logPostFailure;
        _logError = logError;
    }

    public async Task<MacActivityProcessingResult> ProcessAsync(
        DateTimeOffset now,
        AgentRuntimeSettings runtimeSettings,
        string previousForegroundSignature,
        DateTimeOffset? lastScreenshotAt,
        CancellationToken ct)
    {
        var result = new MacActivityProcessingResult(
            HadSuccess: false,
            Unauthorized: false,
            ForegroundSignature: previousForegroundSignature,
            LastScreenshotAt: lastScreenshotAt);

        IReadOnlyList<MonitorInfoDto> monitors = Array.Empty<MonitorInfoDto>();
        IReadOnlyList<ActivitySample> monitorSamples = Array.Empty<ActivitySample>();
        var splitScreenDetected = false;

        try
        {
            monitors = await _appCollector.GetConnectedMonitorsAsync(ct);
            var windows = await _appCollector.GetVisibleWindowsAsync(ct);
            var pipState = await _apiClient.GetPipStateAsync(ct);
            var pipActive = pipState.Success && pipState.Value?.PipActive == true;
            var videoPlaying = pipState.Success && pipState.Value?.VideoPlaying == true;

            monitorSamples = MonitorDetector.BuildMonitorSamples(
                monitors,
                windows,
                pipActive,
                videoPlaying,
                now);

            splitScreenDetected = monitorSamples.Any(sample => sample.IsSplitScreen);
            foreach (var sample in monitorSamples)
            {
                var monitor = monitors.FirstOrDefault(m =>
                    string.Equals(m.MonitorId, sample.MonitorId, StringComparison.OrdinalIgnoreCase));
                if (monitor is null)
                {
                    continue;
                }

                var postResult = await _apiClient.PostActivitySampleAsync(
                    new ActivitySampleRequest(
                        sample.ProcessName,
                        sample.WindowTitle,
                        sample.MonitorId,
                        monitor.Width,
                        monitor.Height,
                        sample.WindowBounds,
                        sample.IsSplitScreen,
                        sample.IsPiPActive,
                        sample.IsVideoPlaying,
                        sample.MonitorCount,
                        sample.AttentionScore,
                        sample.TimestampUtc),
                    ct);

                if (postResult.Success)
                {
                    result = result with { HadSuccess = true, ForegroundSignature = $"{sample.ProcessName}|{sample.WindowTitle}" };
                }
                else
                {
                    _logPostFailure("activity-sample", postResult);
                    if (postResult.IsUnauthorized)
                    {
                        return result with { Unauthorized = true };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logError($"Monitor sample failed: {ex.Message}");
        }

        try
        {
            var currentSignature = monitorSamples.FirstOrDefault() is { } current
                ? $"{current.ProcessName}|{current.WindowTitle}"
                : result.ForegroundSignature;

            var triggerReason = ScreenshotPolicy.TriggerInterval;
            if (!string.IsNullOrWhiteSpace(currentSignature)
                && !string.Equals(currentSignature, previousForegroundSignature, StringComparison.Ordinal))
            {
                triggerReason = ScreenshotPolicy.TriggerAppSwitch;
            }
            else if (splitScreenDetected)
            {
                triggerReason = ScreenshotPolicy.TriggerSplitScreen;
            }

            if (monitors.Count > 0 && ScreenshotPolicy.ShouldCapture(
                    runtimeSettings.EnableScreenshots,
                    result.LastScreenshotAt,
                    now,
                    runtimeSettings.ScreenshotIntervalMinutes,
                    triggerReason))
            {
                var captures = await _screenshotService.CaptureAsync(
                    new ScreenshotCaptureRequest(triggerReason, now, monitors),
                    ct);

                foreach (var capture in captures)
                {
                    if (!ScreenshotFileProtector.TryEncryptFileAtRest(capture.FilePath, out var encryptedPath, out var error))
                    {
                        _logError($"Screenshot encryption skipped: {error}");
                        continue;
                    }

                    var postResult = await _apiClient.PostScreenshotAsync(
                        new ScreenshotEventRequest(
                            capture.MonitorId,
                            encryptedPath,
                            capture.TriggerReason,
                            capture.CapturedAtUtc),
                        ct);

                    if (postResult.Success)
                    {
                        result = result with { HadSuccess = true };
                    }
                    else
                    {
                        _logPostFailure("screenshot", postResult);
                        if (postResult.IsUnauthorized)
                        {
                            return result with { Unauthorized = true };
                        }
                    }
                }

                result = result with { LastScreenshotAt = now };
            }

            var screenshotsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EmployeeTracker",
                "screenshots");
            ScreenshotFileProtector.PurgeExpiredScreenshots(screenshotsRoot, runtimeSettings.ScreenshotRetentionDays);
        }
        catch (Exception ex)
        {
            _logError($"Screenshot flow failed: {ex.Message}");
        }

        return result;
    }
}

internal sealed record MacActivityProcessingResult(
    bool HadSuccess,
    bool Unauthorized,
    string ForegroundSignature,
    DateTimeOffset? LastScreenshotAt);
