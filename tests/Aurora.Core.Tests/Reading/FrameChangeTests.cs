using Aurora.Core.Contracts;
using Aurora.Core.Reading;

namespace Aurora.Core.Tests.Reading;

public sealed class FrameChangeTests
{
    [Fact]
    public void First_frame_is_a_change()
    {
        Assert.True(new FrameChange().Accept(Frames.Solid(1)));
    }

    [Fact]
    public void Same_bytes_are_not_a_change_even_in_a_new_buffer()
    {
        var change = new FrameChange();
        change.Accept(Frames.Solid(1));

        Assert.False(change.Accept(Frames.Solid(1)));
    }

    [Fact]
    public void Different_bytes_are_a_change_and_become_the_reference()
    {
        var change = new FrameChange();
        change.Accept(Frames.Solid(1));

        Assert.True(change.Accept(Frames.Solid(2)));
        Assert.False(change.Accept(Frames.Solid(2)));
    }

    [Fact]
    public void Different_size_or_stride_is_a_change_with_equal_bytes()
    {
        var change = new FrameChange();
        var bytes = new byte[32];
        change.Accept(new Frame(4, 2, 16, bytes));

        Assert.True(change.Accept(new Frame(2, 4, 8, bytes)));
        Assert.True(change.Accept(new Frame(2, 4, 16, bytes)));
    }

    [Fact]
    public void Reset_forgets_the_last_frame()
    {
        var change = new FrameChange();
        change.Accept(Frames.Solid(1));
        change.Reset();

        Assert.True(change.Accept(Frames.Solid(1)));
    }
}
