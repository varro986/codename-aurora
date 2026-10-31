using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Aurora.App;

/// <summary>The tray icon, drawn in code (a filled circle) so the app ships no image asset.</summary>
internal static class TrayIconImage
{
    private const int Size = 32;
    private const int Colour = unchecked((int)0xFF2E7DD7); // BGRA as one little-endian word: opaque blue

    public static WindowIcon Create()
    {
        var bitmap = new WriteableBitmap(new PixelSize(Size, Size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var buffer = bitmap.Lock())
        {
            var pixels = new int[Size * Size];
            const double radius = Size / 2.0;
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var (dx, dy) = (x + 0.5 - radius, y + 0.5 - radius);
                    pixels[(y * Size) + x] = (dx * dx) + (dy * dy) <= radius * radius ? Colour : 0;
                }
            }

            for (var y = 0; y < Size; y++)
            {
                Marshal.Copy(pixels, y * Size, buffer.Address + (y * buffer.RowBytes), Size);
            }
        }

        return new WindowIcon(bitmap);
    }
}
