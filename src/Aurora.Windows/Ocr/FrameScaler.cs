namespace Aurora.Windows.Ocr;

/// <summary>Shrinks a BGRA frame so its longest side fits the OCR limit.</summary>
internal static class FrameScaler
{
    /// <summary>Scale factor (at most 1) that brings the longest side to <paramref name="maxDimension"/>.</summary>
    public static double ScaleFor(int width, int height, int maxDimension)
    {
        var longest = Math.Max(width, height);
        return longest <= maxDimension ? 1.0 : (double)maxDimension / longest;
    }

    /// <summary>Box-averaged copy of a BGRA image into a tightly packed <paramref name="newWidth"/> x <paramref name="newHeight"/> one.</summary>
    public static byte[] Downscale(ReadOnlySpan<byte> bgra, int width, int height, int stride, int newWidth, int newHeight)
    {
        var result = new byte[newWidth * newHeight * 4];
        Span<int> sum = stackalloc int[4];
        for (var y = 0; y < newHeight; y++)
        {
            var y0 = (int)((long)y * height / newHeight);
            var y1 = Math.Max(y0 + 1, (int)((long)(y + 1) * height / newHeight));
            for (var x = 0; x < newWidth; x++)
            {
                var x0 = (int)((long)x * width / newWidth);
                var x1 = Math.Max(x0 + 1, (int)((long)(x + 1) * width / newWidth));
                sum.Clear();
                for (var sy = y0; sy < y1; sy++)
                {
                    for (var sx = x0; sx < x1; sx++)
                    {
                        var i = (sy * stride) + (sx * 4);
                        for (var c = 0; c < 4; c++)
                        {
                            sum[c] += bgra[i + c];
                        }
                    }
                }

                var count = (y1 - y0) * (x1 - x0);
                var o = ((y * newWidth) + x) * 4;
                for (var c = 0; c < 4; c++)
                {
                    result[o + c] = (byte)(sum[c] / count);
                }
            }
        }

        return result;
    }
}
