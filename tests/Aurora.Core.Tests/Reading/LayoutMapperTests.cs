using Aurora.Core.Contracts;
using Aurora.Core.Reading;

namespace Aurora.Core.Tests.Reading;

public sealed class LayoutMapperTests
{
    private static readonly PixelRect Line = new(37, 91, 203, 29);

    private static OcrLine[] One() => [new OcrLine("text", Line)];

    [Fact]
    public void Same_size_only_offsets_by_the_bounds_origin()
    {
        var mapped = LayoutMapper.Map(One(), 800, 600, new PixelRect(100, 50, 800, 600));

        Assert.Equal(new PixelRect(137, 141, 203, 29), mapped[0].Bounds);
        Assert.Equal("text", mapped[0].Text);
    }

    [Fact]
    public void Negative_origin_is_supported()
    {
        var mapped = LayoutMapper.Map(One(), 800, 600, new PixelRect(-1920, -40, 800, 600));

        Assert.Equal(new PixelRect(-1883, 51, 203, 29), mapped[0].Bounds);
    }

    [Theory]
    [InlineData(1.00)]
    [InlineData(1.25)]
    [InlineData(1.50)]
    [InlineData(2.00)]
    public void Scaled_frame_maps_within_one_pixel(double ratio)
    {
        var bounds = new PixelRect(-300, 120, 1000, 700);
        var frameWidth = (int)(bounds.Width * ratio);
        var frameHeight = (int)(bounds.Height * ratio);
        var lines = new[]
        {
            new OcrLine("a", new PixelRect(0, 0, frameWidth, 33)),
            new OcrLine("b", new PixelRect(13, 101, 257, 41)),
            new OcrLine("c", new PixelRect(frameWidth - 77, frameHeight - 19, 77, 19)),
        };

        var mapped = LayoutMapper.Map(lines, frameWidth, frameHeight, bounds);

        for (var i = 0; i < lines.Length; i++)
        {
            var b = lines[i].Bounds;
            var sx = (double)bounds.Width / frameWidth;
            var sy = (double)bounds.Height / frameHeight;
            var m = mapped[i].Bounds;
            Assert.InRange(Math.Abs(m.X - (bounds.X + (b.X * sx))), 0, 1);
            Assert.InRange(Math.Abs(m.Y - (bounds.Y + (b.Y * sy))), 0, 1);
            Assert.InRange(Math.Abs(m.Width - (b.Width * sx)), 0, 1);
            Assert.InRange(Math.Abs(m.Height - (b.Height * sy)), 0, 1);
        }
    }

    [Fact]
    public void Empty_list_maps_to_empty()
    {
        Assert.Empty(LayoutMapper.Map([], 10, 10, new PixelRect(0, 0, 10, 10)));
    }
}
