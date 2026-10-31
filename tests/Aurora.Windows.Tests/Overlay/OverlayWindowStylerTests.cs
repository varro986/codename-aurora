using System.ComponentModel;
using System.Runtime.InteropServices;
using Aurora.Windows.Overlay;

namespace Aurora.Windows.Tests.Overlay;

public sealed class OverlayWindowStylerTests : IDisposable
{
    private const int GwlExStyle = -20;
    private const nint WsExTransparent = 0x20;
    private const nint WsExToolWindow = 0x80;
    private const nint WsExLayered = 0x80000;
    private const nint WsExNoActivate = 0x08000000;
    private const nint WsExAcceptFiles = 0x10;
    private const uint WdaExcludeFromCapture = 0x11;
    private const uint LwaAlpha = 0x2;

    private readonly nint _hwnd;

    public OverlayWindowStylerTests()
    {
        // A never-shown STATIC window: no focus, no input, no visible effect.
        _hwnd = CreateWindowExW(WsExAcceptFiles, "STATIC", "aurora-styler-test", 0, 0, 0, 10, 10, 0, 0, 0, 0);
        Assert.NotEqual(0, _hwnd);
    }

    public void Dispose() => DestroyWindow(_hwnd);

    [Fact]
    public void Apply_adds_overlay_styles_and_keeps_existing_ones()
    {
        new OverlayWindowStyler().Apply(_hwnd);

        var style = GetWindowLongPtrW(_hwnd, GwlExStyle);
        const nint expected = WsExTransparent | WsExLayered | WsExNoActivate | WsExToolWindow | WsExAcceptFiles;
        Assert.Equal(expected, style & expected);
    }

    [Fact]
    public void Apply_excludes_the_window_from_capture()
    {
        new OverlayWindowStyler().Apply(_hwnd);

        Assert.True(GetWindowDisplayAffinity(_hwnd, out var affinity));
        Assert.Equal(WdaExcludeFromCapture, affinity);
    }

    [Fact]
    public void Apply_sets_the_layered_attributes_so_the_window_paints()
    {
        new OverlayWindowStyler().Apply(_hwnd);

        Assert.True(GetLayeredWindowAttributes(_hwnd, out _, out var alpha, out var flags));
        Assert.Equal(255, alpha);
        Assert.Equal(LwaAlpha, flags & LwaAlpha);
    }

    [Fact]
    public void Apply_is_idempotent()
    {
        var styler = new OverlayWindowStyler();
        styler.Apply(_hwnd);
        var first = GetWindowLongPtrW(_hwnd, GwlExStyle);

        styler.Apply(_hwnd);

        Assert.Equal(first, GetWindowLongPtrW(_hwnd, GwlExStyle));
    }

    [Fact]
    public void Apply_throws_the_win32_error_for_an_invalid_handle()
    {
        Assert.Throws<Win32Exception>(() => new OverlayWindowStyler().Apply(0x1));
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(
        nint exStyle, string className, string windowName, uint style, int x, int y, int width, int height,
        nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtrW(nint hwnd, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLayeredWindowAttributes(nint hwnd, out uint colorKey, out byte alpha, out uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowDisplayAffinity(nint hwnd, out uint affinity);
}
