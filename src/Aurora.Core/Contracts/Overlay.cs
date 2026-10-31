namespace Aurora.Core.Contracts;

public interface IOverlay
{
    /// <summary>Draws each line over the original; target bounds and line bounds in screen pixels. Callable from any thread.</summary>
    void Show(PixelRect targetBounds, IReadOnlyList<OcrLine> lines);

    void Hide();
}

/// <summary>
/// Gives a native window the overlay behaviour: click-through, never focused, no taskbar entry, excluded from capture.
/// Implemented per operating system, so the overlay project stays free of native calls.
/// </summary>
public interface IOverlayWindowStyler
{
    void Apply(nint windowHandle);
}
