using System.Runtime.InteropServices;

namespace Aurora.Windows.Tests.Capture;

[StructLayout(LayoutKind.Sequential)]
internal struct WindowClass
{
    public uint Size;
    public uint Style;
    public nint WindowProc;
    public int ClassExtra;
    public int WindowExtra;
    public nint Instance;
    public nint Icon;
    public nint Cursor;
    public nint Background;
    public nint MenuName;
    public nint ClassName;
    public nint SmallIcon;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Message
{
    public nint Window;
    public uint Id;
    public nint WParam;
    public nint LParam;
    public uint Time;
    public int X;
    public int Y;
    public uint Private;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

internal static class NativeMethods
{
    public const uint WsPopup = 0x80000000;
    public const uint WsExTopmost = 0x8;
    public const uint WsExNoActivate = 0x08000000;
    public const int SwShowNoActivate = 4;
    public const uint PmRemove = 1;
    public const uint MonitorDefaultToPrimary = 1;
    public const uint DwmwaExtendedFrameBounds = 9;

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW")]
    public static extern ushort RegisterClass(in WindowClass windowClass);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern nint CreateWindow(
        uint exStyle, string className, string title, uint style, int x, int y, int width, int height,
        nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll", EntryPoint = "PeekMessageW")]
    public static extern bool PeekMessage(out Message message, nint window, uint min, uint max, uint remove);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    public static extern nint DispatchMessage(in Message message);

    [DllImport("user32.dll")]
    public static extern bool DestroyWindow(nint window);

    [DllImport("user32.dll")]
    public static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("gdi32.dll")]
    public static extern nint CreateSolidBrush(uint colorRef);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(nint window, uint attribute, out Rect value, int size);
}
