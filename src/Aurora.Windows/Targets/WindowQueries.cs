using System.Runtime.InteropServices;
using Aurora.Core.Contracts;

namespace Aurora.Windows.Targets;

/// <summary>Read-only questions about top-level windows and monitors, shared by the resolver and the events.</summary>
internal static class WindowQueries
{
    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;

    public static nint RootOf(nint window) => window == 0 ? 0 : NativeMethods.GetAncestor(window, NativeMethods.GA_ROOT);

    public static uint ProcessIdOf(nint window)
    {
        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        return processId;
    }

    public static bool IsOwn(nint window) => ProcessIdOf(window) == OwnProcessId;

    /// <summary>
    /// A window worth attaching to: visible, uncloaked, titled, not a tool window, not the shell, not one of Aurora.
    /// </summary>
    public static unsafe bool IsAttachable(nint window)
    {
        if (!NativeMethods.IsWindowVisible(window)
            || window == NativeMethods.GetShellWindow()
            || NativeMethods.GetWindowTextLength(window) == 0
            || (Interop.NativeMethods.GetWindowLongPtr(window,NativeMethods.GWL_EXSTYLE) & NativeMethods.WS_EX_TOOLWINDOW) != 0
            || IsOwn(window))
        {
            return false;
        }

        return NativeMethods.DwmGetWindowAttribute(window, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) != 0
            || cloaked == 0;
    }

    public static unsafe string TitleOf(nint window)
    {
        var length = NativeMethods.GetWindowTextLength(window);
        if (length == 0)
        {
            return string.Empty;
        }

        var buffer = new char[length + 1];
        fixed (char* text = buffer)
        {
            return new string(text, 0, NativeMethods.GetWindowText(window, text, buffer.Length));
        }
    }

    /// <summary>
    /// The window bounds in screen physical pixels, from the DWM extended frame (what Windows Graphics Capture
    /// captures: no invisible resize border), or the window rectangle when DWM has none.
    /// </summary>
    public static unsafe bool TryGetBounds(nint window, out PixelRect bounds)
    {
        if (NativeMethods.DwmGetWindowAttribute(
                window, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out NativeMethods.Rect rect, sizeof(NativeMethods.Rect)) == 0
            || NativeMethods.GetWindowRect(window, out rect))
        {
            bounds = ToPixelRect(rect);
            return true;
        }

        bounds = default;
        return false;
    }

    /// <summary>The rectangle a minimised window returns to, from its placement.</summary>
    public static unsafe bool TryGetRestoredBounds(nint window, out PixelRect bounds)
    {
        var placement = new NativeMethods.WindowPlacement { Size = sizeof(NativeMethods.WindowPlacement) };
        if (NativeMethods.GetWindowPlacement(window, ref placement))
        {
            bounds = ToPixelRect(placement.NormalPosition);
            return true;
        }

        bounds = default;
        return false;
    }

    /// <summary>Attachable top-level windows, topmost first (EnumWindows order).</summary>
    public static List<nint> AttachableWindows()
    {
        var windows = new List<nint>();
        NativeMethods.EnumWindowsProc callback = (window, _) =>
        {
            if (IsAttachable(window))
            {
                windows.Add(window);
            }

            return 1;
        };
        NativeMethods.EnumWindows(Marshal.GetFunctionPointerForDelegate(callback), 0);
        GC.KeepAlive(callback);
        return windows;
    }

    public static List<(nint Handle, PixelRect Bounds)> Monitors()
    {
        var monitors = new List<(nint, PixelRect)>();
        NativeMethods.MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            if (TryGetMonitorBounds(monitor, out var bounds))
            {
                monitors.Add((monitor, bounds));
            }

            return 1;
        };
        NativeMethods.EnumDisplayMonitors(0, 0, Marshal.GetFunctionPointerForDelegate(callback), 0);
        GC.KeepAlive(callback);
        return monitors;
    }

    public static unsafe bool TryGetMonitorBounds(nint monitor, out PixelRect bounds)
    {
        var info = new NativeMethods.MonitorInfo { Size = sizeof(NativeMethods.MonitorInfo) };
        if (NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            bounds = ToPixelRect(info.Monitor);
            return true;
        }

        bounds = default;
        return false;
    }

    private static PixelRect ToPixelRect(NativeMethods.Rect rect) =>
        new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
}
