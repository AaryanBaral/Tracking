using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Agent.Shared.Models;

namespace Agent.Windows.Native;

internal static class WindowsInput
{
    private const uint MonitorInfoFPrimary = 0x00000001;
    private const uint MonitorDefaultToNearest = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    public static double GetIdleSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info))
        {
            return 0;
        }

        var tickCount = (uint)Environment.TickCount;
        var idleMs = unchecked(tickCount - info.dwTime);
        return idleMs / 1000.0;
    }

    public static ForegroundAppInfo? GetForegroundApp()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return null;
        }

        var appName = GetProcessName(pid);
        if (string.IsNullOrWhiteSpace(appName))
        {
            return null;
        }

        var title = GetWindowTitle(hwnd);
        return new ForegroundAppInfo(appName, title);
    }

    public static IReadOnlyList<MonitorInfoDto> GetConnectedMonitors()
    {
        var monitors = new List<MonitorInfoDto>();

        bool Callback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT monitorRect, IntPtr data)
        {
            var info = new MONITORINFOEX
            {
                cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>(),
                szDevice = string.Empty
            };

            if (!GetMonitorInfo(hMonitor, ref info))
            {
                return true;
            }

            var width = info.rcMonitor.Right - info.rcMonitor.Left;
            var height = info.rcMonitor.Bottom - info.rcMonitor.Top;
            monitors.Add(new MonitorInfoDto(
                string.IsNullOrWhiteSpace(info.szDevice) ? hMonitor.ToString() : info.szDevice,
                info.rcMonitor.Left,
                info.rcMonitor.Top,
                width,
                height,
                (info.dwFlags & MonitorInfoFPrimary) != 0));
            return true;
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);

        return monitors;
    }

    public static IReadOnlyList<VisibleWindowDto> GetVisibleWindows()
    {
        var shellWindow = GetShellWindow();
        var windows = new List<VisibleWindowDto>();
        var zOrder = 0;

        EnumWindows((hWnd, _) =>
        {
            if (hWnd == IntPtr.Zero || hWnd == shellWindow)
            {
                zOrder++;
                return true;
            }

            var isVisible = IsWindowVisible(hWnd);
            var isMinimized = IsIconic(hWnd);
            if (!GetWindowRect(hWnd, out var rect))
            {
                zOrder++;
                return true;
            }

            var width = Math.Max(0, rect.Right - rect.Left);
            var height = Math.Max(0, rect.Bottom - rect.Top);
            if (width == 0 || height == 0)
            {
                zOrder++;
                return true;
            }

            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == 0)
            {
                zOrder++;
                return true;
            }

            var processName = GetProcessName(pid);
            if (string.IsNullOrWhiteSpace(processName))
            {
                zOrder++;
                return true;
            }

            var title = GetWindowTitle(hWnd);
            var monitorId = GetMonitorIdForWindow(hWnd);

            windows.Add(new VisibleWindowDto(
                processName,
                title,
                new WindowBoundsDto(rect.Left, rect.Top, width, height),
                zOrder,
                isMinimized,
                isVisible,
                monitorId));

            zOrder++;
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static string GetMonitorIdForWindow(IntPtr hWnd)
    {
        var monitor = MonitorFromWindow(hWnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return "unknown";
        }

        var info = new MONITORINFOEX
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>(),
            szDevice = string.Empty
        };

        if (!GetMonitorInfo(monitor, ref info))
        {
            return "unknown";
        }

        return string.IsNullOrWhiteSpace(info.szDevice) ? monitor.ToString() : info.szDevice;
    }

    private static string? GetWindowTitle(IntPtr hWnd)
    {
        var length = GetWindowTextLength(hWnd);
        if (length <= 0)
        {
            return null;
        }

        var sb = new StringBuilder(length + 1);
        return GetWindowText(hWnd, sb, sb.Capacity) > 0 ? sb.ToString() : null;
    }

    private static string? GetProcessName(uint pid)
    {
        try
        {
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record ForegroundAppInfo(string AppName, string? WindowTitle);
