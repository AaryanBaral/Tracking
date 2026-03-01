using System.Runtime.InteropServices;
using Agent.Shared.Abstractions;
using Agent.Shared.Models;

namespace Agent.Mac.Collectors;

public sealed class MacAppCollector : IAppCollector
{
    public async Task<AppFocusEvent?> GetFocusedAppAsync(CancellationToken cancellationToken)
    {
        var script = """
            tell application "System Events"
                set frontApp to first application process whose frontmost is true
                set appName to name of frontApp
                set winTitle to ""
                try
                    set winTitle to value of attribute "AXTitle" of front window of frontApp
                on error
                    try
                        set winTitle to name of front window of frontApp
                    end try
                end try
                return appName & "||" & winTitle
            end tell
            """;

        var result = await Shell.RunAsync(
            "/usr/bin/osascript",
            new[] { "-e", script },
            cancellationToken);
        if (string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        var parts = result.Split("||", 2, StringSplitOptions.None);
        var appName = parts[0].Trim();
        var windowTitle = parts.Length > 1 ? parts[1].Trim() : null;
        if (string.IsNullOrWhiteSpace(appName))
        {
            return null;
        }

        return new AppFocusEvent(
            DateTimeOffset.UtcNow,
            appName,
            string.IsNullOrWhiteSpace(windowTitle) ? null : windowTitle);
    }

    // Additive API: callers that need split-screen/multi-window state can use this richer snapshot.
    public async Task<IReadOnlyList<VisibleWindowDto>> GetVisibleWindowsAsync(CancellationToken cancellationToken)
    {
        var monitors = await GetConnectedMonitorsAsync(cancellationToken);
        var script = """
            tell application "System Events"
                set AppleScript's text item delimiters to "\n"
                set outLines to {}
                set procList to every application process whose background only is false
                repeat with p in procList
                    set pName to name of p
                    try
                        set winList to every window of p
                        repeat with w in winList
                            set winTitle to ""
                            set pxy to {0, 0}
                            set wsz to {0, 0}
                            set minimizedFlag to false
                            set visibleFlag to true
                            try
                                set winTitle to name of w
                            end try
                            try
                                set pxy to position of w
                            end try
                            try
                                set wsz to size of w
                            end try
                            try
                                set minimizedFlag to value of attribute "AXMinimized" of w
                            end try
                            try
                                set visibleFlag to value of attribute "AXVisible" of w
                            end try

                            set end of outLines to pName & "||" & winTitle & "||" & (item 1 of pxy) & "||" & (item 2 of pxy) & "||" & (item 1 of wsz) & "||" & (item 2 of wsz) & "||" & minimizedFlag & "||" & visibleFlag
                        end repeat
                    end try
                end repeat
                return outLines as string
            end tell
            """;

        var output = await Shell.RunAsync(
            "/usr/bin/osascript",
            new[] { "-e", script },
            cancellationToken);

        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<VisibleWindowDto>();
        }

        var monitorList = monitors.ToList();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var windows = new List<VisibleWindowDto>(lines.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var parts = lines[i].Split("||", StringSplitOptions.None);
            if (parts.Length < 8)
            {
                continue;
            }

            var processName = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(processName))
            {
                continue;
            }

            _ = int.TryParse(parts[2], out var x);
            _ = int.TryParse(parts[3], out var y);
            _ = int.TryParse(parts[4], out var width);
            _ = int.TryParse(parts[5], out var height);
            _ = bool.TryParse(parts[6], out var minimized);
            _ = bool.TryParse(parts[7], out var visible);

            if (width <= 0 || height <= 0)
            {
                continue;
            }

            var monitorId = ResolveMonitorId(x, y, width, height, monitorList);
            windows.Add(new VisibleWindowDto(
                processName,
                string.IsNullOrWhiteSpace(parts[1]) ? null : parts[1],
                new WindowBoundsDto(x, y, width, height),
                i,
                minimized,
                visible,
                monitorId));
        }

        return windows;
    }

    // Uses CoreGraphics display APIs to provide monitor identity and bounds on macOS.
    public Task<IReadOnlyList<MonitorInfoDto>> GetConnectedMonitorsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const uint maxDisplays = 16;
        var displays = new uint[maxDisplays];
        var result = CGGetActiveDisplayList(maxDisplays, displays, out var displayCount);
        if (result != 0 || displayCount == 0)
        {
            return Task.FromResult<IReadOnlyList<MonitorInfoDto>>(Array.Empty<MonitorInfoDto>());
        }

        var mainDisplay = CGMainDisplayID();
        var monitors = new List<MonitorInfoDto>((int)displayCount);

        for (var i = 0; i < displayCount; i++)
        {
            var id = displays[i];
            var bounds = CGDisplayBounds(id);
            monitors.Add(new MonitorInfoDto(
                id.ToString(),
                (int)bounds.Origin.X,
                (int)bounds.Origin.Y,
                (int)bounds.Size.Width,
                (int)bounds.Size.Height,
                id == mainDisplay));
        }

        return Task.FromResult<IReadOnlyList<MonitorInfoDto>>(monitors);
    }

    private static string ResolveMonitorId(
        int x,
        int y,
        int width,
        int height,
        IReadOnlyList<MonitorInfoDto> monitors)
    {
        if (monitors.Count == 0)
        {
            return "unknown";
        }

        var centerX = x + (width / 2);
        var centerY = y + (height / 2);
        foreach (var monitor in monitors)
        {
            if (centerX >= monitor.X
                && centerX < monitor.X + monitor.Width
                && centerY >= monitor.Y
                && centerY < monitor.Y + monitor.Height)
            {
                return monitor.MonitorId;
            }
        }

        return monitors[0].MonitorId;
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern int CGGetActiveDisplayList(
        uint maxDisplays,
        [Out] uint[] activeDisplays,
        out uint displayCount);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern uint CGMainDisplayID();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern CGRect CGDisplayBounds(uint display);

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;
    }
}
