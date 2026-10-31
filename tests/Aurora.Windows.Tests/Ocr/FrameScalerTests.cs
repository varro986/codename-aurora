using Aurora.Windows.Ocr;

namespace Aurora.Windows.Tests.Ocr;

public sealed class FrameScalerTests
{
    [Theory]
    [InlineData(100, 50, 100, 1.0)]
    [InlineData(100, 50, 200, 1.0)]
    [InlineData(100, 50, 50, 0.5)]
    [InlineData(50, 100, 25, 0.25)]
    public void ScaleFor_shrinks_the_longest_side_to_the_limit(int width, int height, int limit, double expected)
    {
        Assert.Equal(expected, FrameScaler.ScaleFor(width, height, limit));
    }

    [Fact]
    public void Downscale_ignores_the_padding_at_the_end_of_each_row()
    {
        const int width = 4, height = 4, stride = (width * 4) + 8;
        var padded = new byte[stride * height];
        Array.Fill(padded, (byte)0xEE);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Left half 0, right half 100 on every channel.
                padded.AsSpan((y * stride) + (x * 4), 4).Fill(x < 2 ? (byte)0 : (byte)100);
            }
        }

        var result = FrameScaler.Downscale(padded, width, height, stride, newWidth: 2, newHeight: 2);

        Assert.Equal([0, 0, 0, 0, 100, 100, 100, 100, 0, 0, 0, 0, 100, 100, 100, 100], result);
    }
}
