using Aurora.Core.Contracts;

namespace Aurora.Core.Reading;

public static class LayoutMapper
{
    /// <summary>
    /// Maps lines from frame pixels to screen pixels: scaled when the frame size differs from the bounds size,
    /// then offset by the bounds origin. Edges are rounded one by one, so each is within half a pixel of the exact value.
    /// </summary>
    public static IReadOnlyList<OcrLine> Map(IReadOnlyList<OcrLine> lines, int frameWidth, int frameHeight, PixelRect screenBounds)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameHeight);
        var mapped = new OcrLine[lines.Count];
        for (var i = 0; i < mapped.Length; i++)
        {
            var b = lines[i].Bounds;
            var left = ScaleEdge(b.X, frameWidth, screenBounds.Width, screenBounds.X);
            var top = ScaleEdge(b.Y, frameHeight, screenBounds.Height, screenBounds.Y);
            var right = ScaleEdge(b.Right, frameWidth, screenBounds.Width, screenBounds.X);
            var bottom = ScaleEdge(b.Bottom, frameHeight, screenBounds.Height, screenBounds.Y);
            mapped[i] = lines[i] with { Bounds = new PixelRect(left, top, right - left, bottom - top) };
        }

        return mapped;
    }

    private static int ScaleEdge(int value, int frameSize, int boundsSize, int origin) =>
        origin + (int)Math.Round((double)value * boundsSize / frameSize, MidpointRounding.AwayFromZero);
}
