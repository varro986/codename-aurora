using Aurora.Core.Contracts;
using Aurora.Windows.Targets;
using Aurora.Windows.Tests.Support;

namespace Aurora.Windows.Tests.Targets;

public sealed class WindowsTargetResolverTests
{
    private const KeyModifiers ProcessModifier = KeyModifiers.Control;
    private const KeyModifiers MonitorModifier = KeyModifiers.Control | KeyModifiers.Shift;

    private static readonly WindowsTargetResolver Resolver = new(ProcessModifier, MonitorModifier);

    [Fact]
    public void Resolve_a_point_outside_every_monitor_gives_null()
    {
        var outside = new PixelPoint(-30000, -30000);

        Assert.Null(Resolver.Resolve(outside, KeyModifiers.None));
        Assert.Null(Resolver.Resolve(outside, ProcessModifier));
        Assert.Null(Resolver.Resolve(outside, MonitorModifier));
    }

    [Fact]
    public void List_contains_at_least_one_titled_monitor()
    {
        var monitors = Resolver.List().Where(choice => choice.Target is MonitorTarget).ToList();

        Assert.NotEmpty(monitors);
        Assert.All(monitors, monitor => Assert.StartsWith("Monitor ", monitor.Title, StringComparison.Ordinal));
    }

    [Fact]
    public void Describe_gives_the_same_name_as_list_for_the_monitors()
    {
        var monitors = Resolver.List().Where(choice => choice.Target is MonitorTarget).ToList();

        Assert.All(monitors, monitor => Assert.Equal(monitor.Title, Resolver.Describe(monitor.Target)));
    }

    [Fact]
    public void Describe_names_a_process_by_its_executable()
    {
        var name = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

        Assert.Equal($"All windows of {name}", Resolver.Describe(new ProcessTarget(Environment.ProcessId)));
    }

    [Fact]
    public void Describe_names_a_target_that_is_gone_generically()
    {
        Assert.Equal("Window", Resolver.Describe(new WindowTarget(0x7FFFFFF0)));
        Assert.Equal("Monitor", Resolver.Describe(new MonitorTarget(0x7FFFFFF0)));
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task List_contains_the_window_its_process_and_a_monitor()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, window) = await TestNative.StartDesktopAsync(ct);
        await using (target)
        {
            var choices = Resolver.List();

            Assert.Contains(choices, c => c.Target is WindowTarget w && w.Handle == window && c.Title == "Aurora Target Desktop");
            Assert.Single(choices, c => c.Target is ProcessTarget p && p.ProcessId == target.Process.Id);
            Assert.Contains(choices, c => c.Target is MonitorTarget);
        }
    }

    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Resolve_with_the_monitor_modifier_gives_the_monitor_under_the_click()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, window) = await TestNative.StartDesktopAsync(ct);
        await using (target)
        {
            var click = CenterOf(window);

            var resolved = Resolver.Resolve(click, MonitorModifier);

            var monitor = Assert.IsType<MonitorTarget>(resolved);
            Assert.Contains(Resolver.List(), c => c.Target == monitor);
        }
    }

    [Fact]
    [Trait("Category", "Desktop-Foreground")]
    public async Task Resolve_without_modifier_gives_the_window_under_the_click()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, window) = await TestNative.StartDesktopAsync(ct);
        await using (target)
        {
            await Foreground.BringAsync(window, ct);

            Assert.Equal(new WindowTarget(window), Resolver.Resolve(CenterOf(window), KeyModifiers.None));
        }
    }

    [Fact]
    [Trait("Category", "Desktop-Foreground")]
    public async Task Resolve_with_the_process_modifier_gives_the_process_of_the_window()
    {
        var ct = TestContext.Current.CancellationToken;
        var (target, window) = await TestNative.StartDesktopAsync(ct);
        await using (target)
        {
            await Foreground.BringAsync(window, ct);

            Assert.Equal(new ProcessTarget(target.Process.Id), Resolver.Resolve(CenterOf(window), ProcessModifier));
        }
    }

    private static PixelPoint CenterOf(nint window)
    {
        var bounds = TestNative.BoundsOf(window);
        return new PixelPoint(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
    }
}
