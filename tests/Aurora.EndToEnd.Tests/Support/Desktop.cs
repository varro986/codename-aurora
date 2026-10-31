using System.Runtime.InteropServices;

namespace Aurora.EndToEnd.Tests.Support;

/// <summary>Win32 calls the tests need to inject input and to look at windows, in physical pixels.</summary>
internal static class Desktop
{
    public const int WsExTransparent = 0x20;
    public const int WsExLayered = 0x80000;
    public const int WsExNoActivate = 0x8000000;
    public const int WsExToolWindow = 0x80;

    private const int GwlExStyle = -20;
    private const int PerMonitorAwareV2 = -4;
    private const uint ExtendedFrameBounds = 9;
    private const uint GaRoot = 2;
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseLeftDown = 0x2;
    private const uint MouseLeftUp = 0x4;
    private const uint KeyUp = 0x2;
    private const uint SwpNoSize = 0x1;
    private const uint SwpNoZOrder = 0x4;
    private const uint SwpNoActivate = 0x10;
    private const ushort VkShift = 0x10;
    private const ushort VkControl = 0x11;
    private const ushort VkMenu = 0x12;
    private const ushort VkEscape = 0x1B;
    private const ushort VkF20 = 0x83;

    // Without it the test process sees virtualised coordinates; set once, before any window is measured.
    static Desktop() => SetProcessDpiAwarenessContext(PerMonitorAwareV2);

    public readonly record struct Bounds(int X, int Y, int Width, int Height)
    {
        public (int X, int Y) Center => (X + Width / 2, Y + Height / 2);
    }

    /// <summary>The DWM extended frame bounds: the visible rectangle of a window, shadow excluded.</summary>
    public static Bounds BoundsOf(nint window)
    {
        Marshal.ThrowExceptionForHR(DwmGetWindowAttribute(window, ExtendedFrameBounds, out var rect, Marshal.SizeOf<Rect>()));
        return new Bounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public static bool HasExtendedStyles(nint window, int styles) => ((int)GetWindowLongPtrW(window, GwlExStyle) & styles) == styles;

    /// <summary>The opacity of a layered window, or null when the window is not layered.</summary>
    public static byte? LayeredAlpha(nint window) =>
        GetLayeredWindowAttributes(window, out _, out var alpha, out _) ? alpha : null;

    public static uint ProcessOf(nint window)
    {
        _ = GetWindowThreadProcessId(window, out var process);
        return process;
    }

    public static nint ForegroundWindow() => GetForegroundWindow();

    public static nint RootAt((int X, int Y) point) => GetAncestor(WindowFromPoint(new Point(point.X, point.Y)), GaRoot);

    public static nint RootOf(nint window) => GetAncestor(window, GaRoot);

    /// <summary>The visible top-level windows of a process.</summary>
    public static IReadOnlyList<nint> VisibleWindowsOf(int processId)
    {
        var found = new List<nint>();
        EnumWindows(
            (window, _) =>
            {
                if (IsWindowVisible(window) && ProcessOf(window) == processId)
                {
                    found.Add(window);
                }

                return true;
            },
            0);
        return found;
    }

    public static void MoveTo(nint window, int x, int y) =>
        SetWindowPos(window, 0, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);

    /// <summary>
    /// Brings a window to the foreground. A background process may not: joining the input queue of the current
    /// foreground thread gives the right.
    /// </summary>
    public static Task ForegroundAsync(nint window, CancellationToken cancellationToken) =>
        Aurora.Windows.Tests.Support.Foreground.BringAsync(window, cancellationToken);

    /// <summary>Presses the hotkey: modifiers down, F20, and everything released also when the injection fails.</summary>
    public static void PressHotkey()
    {
        try
        {
            SendKeys([(VkControl, false), (VkMenu, false), (VkShift, false), (VkF20, false), (VkF20, true)]);
        }
        finally
        {
            SendKeys([(VkShift, true), (VkMenu, true), (VkControl, true)]);
        }
    }

    public static void PressEscape() => SendKeys([(VkEscape, false), (VkEscape, true)]);

    public static void MoveCursor((int X, int Y) point) => SetCursorPos(point.X, point.Y);

    public static async Task ClickAsync((int X, int Y) point, CancellationToken cancellationToken)
    {
        SetCursorPos(point.X, point.Y);
        // The window under the pointer takes the focus on pointer enter: give it the moment it needs.
        await Task.Delay(150, cancellationToken);
        try
        {
            SendMouse(MouseLeftDown);
            await Task.Delay(50, cancellationToken);
        }
        finally
        {
            SendMouse(MouseLeftUp);
        }
    }

    private static void SendKeys(IReadOnlyList<(ushort Key, bool Up)> keys) =>
        Send([.. keys.Select(k => new Input { Type = InputKeyboard, Data = new InputData { Keyboard = new KeyboardInput { Key = k.Key, Flags = k.Up ? KeyUp : 0 } } })]);

    private static void SendMouse(uint flags) =>
        Send([new Input { Type = InputMouse, Data = new InputData { Mouse = new MouseInput { Flags = flags } } }]);

    private static void Send(Input[] inputs)
    {
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
        {
            throw new InvalidOperationException("The input was not injected (a window of higher privilege in front?).");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Key;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nint Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint Data;
        public uint Flags;
        public uint Time;
        public nint Extra;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputData Data;
    }

    private delegate bool EnumWindowsProc(nint window, nint parameter);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(nint context);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    private static extern nint GetWindowLongPtrW(nint window, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLayeredWindowAttributes(nint window, out uint colorKey, out byte alpha, out uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, uint attribute, out Rect value, int size);
}
