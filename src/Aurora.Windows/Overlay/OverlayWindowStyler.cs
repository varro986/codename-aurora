using System.ComponentModel;
using System.Runtime.InteropServices;
using Aurora.Core.Contracts;

namespace Aurora.Windows.Overlay;

/// <summary>Click-through, never activated, hidden from the taskbar and excluded from screen capture.</summary>
public sealed class OverlayWindowStyler : IOverlayWindowStyler
{
    private const nint AddedStyles = NativeMethods.WsExTransparent | NativeMethods.WsExLayered
        | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow;

    public void Apply(nint windowHandle)
    {
        var current = NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GwlExStyle);
        ThrowIfFailed(current == 0);

        // The previous style can legitimately be 0, so only a non-zero error code tells a failure.
        var previous = NativeMethods.SetWindowLongPtr(windowHandle, NativeMethods.GwlExStyle, current | AddedStyles);
        ThrowIfFailed(previous == 0);

        // A layered window paints nothing until its attributes are set; fully opaque keeps the content as drawn.
        ThrowIfFailed(!NativeMethods.SetLayeredWindowAttributes(windowHandle, 0, byte.MaxValue, NativeMethods.LwaAlpha));

        ThrowIfFailed(!NativeMethods.SetWindowDisplayAffinity(windowHandle, NativeMethods.WdaExcludeFromCapture));
    }

    private static void ThrowIfFailed(bool suspect)
    {
        var error = Marshal.GetLastPInvokeError();
        if (suspect && error != 0)
        {
            throw new Win32Exception(error);
        }
    }
}
