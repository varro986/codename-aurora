using Avalonia.Media;

namespace Aurora.Overlay;

/// <summary>Visual values of the overlay and the pick layer, kept together so they are tuned in one place.</summary>
internal static class OverlayStyle
{
    // OCR boxes include ascenders and descenders, so the glyphs are smaller than the box height.
    public const double FontHeightRatio = 0.78;

    // Opaque black on white: always readable and hides the original text underneath.
    public static readonly IBrush LineBackground = Brushes.White;
    public static readonly IBrush LineForeground = Brushes.Black;

    // Light enough to keep the screen readable, visible enough to show that a pick is waiting.
    public static readonly IBrush PickDim = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0));
}
