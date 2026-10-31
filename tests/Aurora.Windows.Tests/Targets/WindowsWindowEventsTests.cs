using Aurora.Core.Contracts;
using Aurora.Windows.Targets;
using Aurora.Windows.Tests.Support;

namespace Aurora.Windows.Tests.Targets;

public sealed class WindowsWindowEventsTests
{
    // Frame bounds exclude the invisible border but include the title bar: a client size is within this margin.
    private const int FrameMargin = 120;

    private static nint FirstMonitor() =>
        new WindowsTargetResolver(KeyModifiers.Control, KeyModifiers.Shift).List()
            .Select(c => c.Target).OfType<MonitorTarget>().First().Handle;

    [Fact]
    public async Task Watch_a_monitor_raises_its_bounds_at_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var handle = FirstMonitor();
        var bounds = TestNative.MonitorBounds(handle);
        using var events = new WindowsWindowEvents();
        using var log = new EventLog(events);

        events.Watch(new MonitorTarget(handle));

        var e = await log.WaitAsync(_ => true, ct);
        Assert.Equal(new TargetEvent(TargetEventKind.ForegroundGained, handle, bounds), e);
    }

    [Fact]
    public async Task Watch_a_window_that_does_not_exist_raises_closed()
    {
        using var events = new WindowsWindowEvents();
        using var log = new EventLog(events);

        events.Watch(new WindowTarget(0x12345));

        await log.WaitAsync(e => e.Kind == TargetEventKind.Closed, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Watch_null_and_dispose_twice_do_not_throw()
    {
        var events = new WindowsWindowEvents();
        events.Watch(null);

        events.Dispose();
        events.Dispose();
    }

    [Fact]
    public void Watch_after_dispose_throws_instead_of_hanging()
    {
        var events = new WindowsWindowEvents();
        events.Dispose();

        Assert.Throws<ObjectDisposedException>(() => events.Watch(new WindowTarget(0x12345)));
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Watch_a_window_raises_the_current_state_with_its_bounds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, window) = await TestNative.StartDesktopAsync(ct);
        await using (target)
        {
            using var events = new WindowsWindowEvents();
            using var log = new EventLog(events);

            events.Watch(new WindowTarget(window));

            var e = await log.WaitAsync(_ => true, ct);
            Assert.True(e.Kind is TargetEventKind.ForegroundGained or TargetEventKind.ForegroundLost);
            Assert.Equal(window, e.Window);
            Assert.Equal(TestNative.BoundsOf(window), e.Bounds);
        }
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Move_raises_moved_with_the_new_position()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, window) = await TestNative.StartDesktopAsync(ct);
        await using (target)
        {
            using var events = new WindowsWindowEvents();
            using var log = new EventLog(events);
            events.Watch(new WindowTarget(window));

            await target.SendAsync("move 300 250", ct);

            var e = await log.WaitAsync(
                e => e.Kind == TargetEventKind.Moved && e.Bounds.X is >= 300 and < 300 + FrameMargin, ct);
            Assert.Equal(window, e.Window);
            Assert.InRange(e.Bounds.Y, 250, 250 + FrameMargin);
        }
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Resize_raises_moved_with_the_new_size()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, window) = await TestNative.StartDesktopAsync(ct);
        await using (target)
        {
            using var events = new WindowsWindowEvents();
            using var log = new EventLog(events);
            events.Watch(new WindowTarget(window));

            await target.SendAsync("resize 640 360", ct);

            var e = await log.WaitAsync(
                e => e.Kind == TargetEventKind.Moved
                    && e.Bounds.Width is >= 640 and < 640 + FrameMargin
                    && e.Bounds.Height is >= 360 and < 360 + FrameMargin,
                ct);
            Assert.Equal(window, e.Window);
        }
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Exit_raises_closed_for_a_window_target()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, window) = await TestNative.StartDesktopAsync(ct);
        await using (TestNative.TolerateExit(target))
        {
            using var events = new WindowsWindowEvents();
            using var log = new EventLog(events);
            events.Watch(new WindowTarget(window));

            await target.SendAsync("exit", ct);

            var e = await log.WaitAsync(e => e.Kind == TargetEventKind.Closed, ct);
            Assert.Equal(window, e.Window);
            Assert.False(e.Bounds.IsEmpty);
        }
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Exit_raises_closed_for_a_process_target_when_the_process_ends()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, _) = await TestNative.StartDesktopAsync(ct);
        await using (TestNative.TolerateExit(target))
        {
            using var events = new WindowsWindowEvents();
            using var log = new EventLog(events);
            events.Watch(new ProcessTarget(target.Process.Id));

            await target.SendAsync("exit", ct);

            await log.WaitAsync(e => e.Kind == TargetEventKind.Closed, ct);
        }
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Watch_replaces_the_previous_target()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, window) = await TestNative.StartDesktopAsync(ct);
        await using (TestNative.TolerateExit(target))
        {
            using var events = new WindowsWindowEvents();
            using var log = new EventLog(events);
            events.Watch(new WindowTarget(window));
            events.Watch(new MonitorTarget(FirstMonitor()));

            await target.SendAsync("exit", ct);
            await Task.Delay(500, ct);

            Assert.DoesNotContain(log.Snapshot(), e => e.Kind == TargetEventKind.Closed);
        }
    }

    [Fact]
    [Trait("Category", "Desktop-Foreground")]
    public async Task Bringing_a_window_to_the_front_raises_foreground_gained()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, window) = await TestNative.StartDesktopAsync(ct);
        await using (target)
        {
            using var events = new WindowsWindowEvents();
            using var log = new EventLog(events);
            events.Watch(new WindowTarget(window));

            await Foreground.BringAsync(window, ct);

            await log.WaitAsync(e => e.Kind == TargetEventKind.ForegroundGained && e.Window == window, ct);
        }
    }

    [Fact]
    [Trait("Category", "Desktop-Foreground")]
    public async Task Minimise_and_restore_raise_minimised_and_restored()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, window) = await TestNative.StartDesktopAsync(ct);
        await using (target)
        {
            using var events = new WindowsWindowEvents();
            using var log = new EventLog(events);
            events.Watch(new WindowTarget(window));

            await target.SendAsync("minimize", ct);
            var minimised = await log.WaitAsync(e => e.Kind == TargetEventKind.Minimised, ct);
            await target.SendAsync("restore", ct);
            var restored = await log.WaitAsync(e => e.Kind == TargetEventKind.Restored, ct);

            Assert.Equal(window, minimised.Window);
            Assert.False(minimised.Bounds.IsEmpty);
            Assert.Equal(window, restored.Window);
            Assert.False(restored.Bounds.IsEmpty);
            Assert.True(restored.Bounds.X > -30000);
        }
    }

    [Fact]
    [Trait("Category", "Desktop-Foreground")]
    public async Task Second_window_raises_window_added_for_a_process_target()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, _) = await TestNative.StartDesktopAsync(ct);
        await using (target)
        {
            using var events = new WindowsWindowEvents();
            using var log = new EventLog(events);
            events.Watch(new ProcessTarget(target.Process.Id));

            await target.SendAsync("second-window Second", ct);

            var second = await TestNative.FindWindowAsync("Aurora Target Desktop Second", ct);
            var added = await log.WaitAsync(e => e.Kind == TargetEventKind.WindowAdded, ct);
            Assert.Equal(second, added.Window);
            Assert.False(added.Bounds.IsEmpty);
        }
    }

    [Fact]
    [Trait("Category", "Desktop-Foreground")]
    public async Task Closing_the_foreground_window_of_a_process_passes_the_foreground_to_the_next_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, main) = await TestNative.StartDesktopAsync(ct);
        await using (target)
        {
            await target.SendAsync("second-window Second", ct);
            var second = await TestNative.FindWindowAsync("Aurora Target Desktop Second", ct);
            await Foreground.BringAsync(second, ct);
            using var events = new WindowsWindowEvents();
            using var log = new EventLog(events);
            events.Watch(new ProcessTarget(target.Process.Id));
            await log.WaitAsync(e => e.Kind == TargetEventKind.ForegroundGained && e.Window == second, ct);

            TestNative.Close(second);

            await log.WaitAsync(e => e.Kind == TargetEventKind.ForegroundGained && e.Window == main, ct);
        }
    }
}
