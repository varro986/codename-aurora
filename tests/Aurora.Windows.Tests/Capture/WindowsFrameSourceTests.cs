using Aurora.Core.Contracts;
using Aurora.Windows.Capture;
using Aurora.Windows.Overlay;
using Aurora.Windows.Tests.Support;

namespace Aurora.Windows.Tests.Capture;

public sealed class WindowsFrameSourceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Prompt = TimeSpan.FromSeconds(1);
    private const int SizeTolerance = 2;

    private readonly WindowsFrameSourceFactory _factory = new(TimeSpan.FromMilliseconds(100));

    [Fact]
    public async Task NextAsync_throws_when_already_cancelled()
    {
        await using var source = _factory.Create(new WindowTarget(1));
        using var cancelled = new CancellationTokenSource(0);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.NextAsync(cancelled.Token));
    }

    [Fact]
    public async Task DisposeAsync_is_idempotent_and_the_source_is_gone_afterwards()
    {
        var source = _factory.Create(new WindowTarget(1));

        await source.DisposeAsync();
        await source.DisposeAsync();

        Assert.Null(await source.NextAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task NextAsync_returns_null_for_a_window_that_does_not_exist()
    {
        await using var source = _factory.Create(new WindowTarget(0x7FFFFFF0));

        Assert.Null(await source.NextAsync(TestContext.Current.CancellationToken).WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task NextAsync_returns_null_for_a_window_closed_before_its_first_frame()
    {
        using var cancel = new CancellationTokenSource(Timeout);
        var target = await SampleTarget.StartAsync(SampleTarget.Desktop, ["Aurora capture test"], cancel.Token);
        var window = await target.MainWindowAsync(cancel.Token);
        await using var source = _factory.Create(new WindowTarget(window));

        await target.DisposeAsync();

        Assert.Null(await source.NextAsync(cancel.Token));
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Window_frames_have_the_window_size_and_the_light_background()
    {
        using var cancel = new CancellationTokenSource(Timeout);
        await using var target = await SampleTarget.StartAsync(SampleTarget.Desktop, ["Aurora capture test"], cancel.Token);
        var window = await target.MainWindowAsync(cancel.Token);
        await using var source = _factory.Create(new WindowTarget(window));

        // The first frames can precede the first paint of the window.
        Frame? frame;
        do
        {
            frame = await source.NextAsync(cancel.Token);
            Assert.NotNull(frame);
            await Task.Delay(100, cancel.Token);
        }
        while (LightFraction(frame) <= 0.5);

        var bounds = ExtendedFrameBounds(window);
        Assert.InRange(frame.Width, bounds.Width - SizeTolerance, bounds.Width + SizeTolerance);
        Assert.InRange(frame.Height, bounds.Height - SizeTolerance, bounds.Height + SizeTolerance);
        Assert.Equal(frame.Width * 4, frame.Stride);
        Assert.Equal(frame.Stride * frame.Height, frame.Bgra.Length);
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Consecutive_calls_on_unchanged_content_both_return_promptly()
    {
        using var cancel = new CancellationTokenSource(Timeout);
        await using var target = await SampleTarget.StartAsync(SampleTarget.Desktop, ["Aurora capture test"], cancel.Token);
        await using var source = _factory.Create(new WindowTarget(await target.MainWindowAsync(cancel.Token)));
        Assert.NotNull(await source.NextAsync(cancel.Token));

        var first = await source.NextAsync(TestContext.Current.CancellationToken).WaitAsync(Prompt, TestContext.Current.CancellationToken);
        var second = await source.NextAsync(TestContext.Current.CancellationToken).WaitAsync(Prompt, TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task A_later_frame_differs_after_the_text_changes()
    {
        using var cancel = new CancellationTokenSource(Timeout);
        await using var target = await SampleTarget.StartAsync(SampleTarget.Desktop, ["Aurora capture test"], cancel.Token);
        await using var source = _factory.Create(new WindowTarget(await target.MainWindowAsync(cancel.Token)));
        var before = await source.NextAsync(cancel.Token);
        Assert.NotNull(before);

        Assert.Equal("ok", await target.SendAsync("set Something else entirely|on two lines", cancel.Token));

        Frame? after;
        do
        {
            await Task.Delay(100, cancel.Token);
            after = await source.NextAsync(cancel.Token);
            Assert.NotNull(after);
        }
        while (after.Bgra.Span.SequenceEqual(before.Bgra.Span));
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task NextAsync_returns_null_once_the_target_exits()
    {
        using var cancel = new CancellationTokenSource(Timeout);
        await using var target = await SampleTarget.StartAsync(SampleTarget.Desktop, ["Aurora capture test"], cancel.Token);
        await using var source = _factory.Create(new WindowTarget(await target.MainWindowAsync(cancel.Token)));
        Assert.NotNull(await source.NextAsync(cancel.Token));

        await target.SendAsync("exit", cancel.Token);

        Frame? frame;
        do
        {
            await Task.Delay(100, cancel.Token);
            frame = await source.NextAsync(cancel.Token);
        }
        while (frame is not null);
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Monitor_frames_leave_out_a_window_excluded_from_capture()
    {
        using var colored = ColorWindow.Show();
        var monitor = new MonitorTarget(NativeMethods.MonitorFromWindow(colored.Handle, NativeMethods.MonitorDefaultToPrimary));
        using var cancel = new CancellationTokenSource(Timeout);

        var visible = await MarkerPixelsAsync(monitor, cancel.Token);
        Assert.True(visible > 1000, "The control capture should show the window.");

        new OverlayWindowStyler().Apply(colored.Handle);

        Assert.Equal(0, await MarkerPixelsAsync(monitor, cancel.Token));
    }

    private async Task<int> MarkerPixelsAsync(MonitorTarget monitor, CancellationToken cancellationToken)
    {
        await using var source = _factory.Create(monitor);
        var frame = await source.NextAsync(cancellationToken);
        Assert.NotNull(frame);
        var bytes = frame.Bgra.Span;
        var count = 0;
        for (var i = 0; i < bytes.Length; i += 4)
        {
            if (bytes[i] == ColorWindow.Blue && bytes[i + 1] == ColorWindow.Green && bytes[i + 2] == ColorWindow.Red)
            {
                count++;
            }
        }

        return count;
    }

    private static double LightFraction(Frame frame)
    {
        var bytes = frame.Bgra.Span;
        var light = 0;
        for (var i = 0; i < bytes.Length; i += 4)
        {
            if (bytes[i] > 200 && bytes[i + 1] > 200 && bytes[i + 2] > 200)
            {
                light++;
            }
        }

        return (double)light / (frame.Width * frame.Height);
    }

    private static (int Width, int Height) ExtendedFrameBounds(nint window)
    {
        Assert.Equal(0, NativeMethods.DwmGetWindowAttribute(
            window, NativeMethods.DwmwaExtendedFrameBounds, out var rect, System.Runtime.InteropServices.Marshal.SizeOf<Rect>()));
        return (rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
}
