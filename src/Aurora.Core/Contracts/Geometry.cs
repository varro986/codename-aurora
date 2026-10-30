namespace Aurora.Core.Contracts;

/// <summary>A point in physical screen pixels.</summary>
public readonly record struct PixelPoint(int X, int Y);

/// <summary>A rectangle in physical pixels; screen or frame coordinates depending on who produces it.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public PixelRect Offset(int dx, int dy) => this with { X = X + dx, Y = Y + dy };
}
