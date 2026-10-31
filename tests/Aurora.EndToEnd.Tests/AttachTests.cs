using System.Diagnostics;
using Aurora.EndToEnd.Tests.Support;
using Aurora.Windows.Tests.Support;

namespace Aurora.EndToEnd.Tests;

/// <summary>The whole app against the sample targets, driven by injected input.</summary>
public sealed class AttachTests
{
    private const int TolerancePixels = 4;
    private static readonly TimeSpan FollowTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan GoneTimeout = TimeSpan.FromSeconds(3);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // AC-1.1
    [Theory]
    [Trait("Category", "Desktop")]
    [InlineData(SampleTarget.Desktop)]
    [InlineData(SampleTarget.Console)]
    public async Task Hotkey_then_click_attaches_and_shows_the_overlay_over_the_target(string targetName)
    {
        await using var session = await Session.StartAsync(targetName);

        await session.AttachAsync();

        var overlay = await session.OverlayAsync();
        AssertOver(session.TargetBounds(), Desktop.BoundsOf(overlay));
        if (targetName == SampleTarget.Desktop)
        {
            Assert.Contains("Attached to Aurora Target Desktop.", session.App.Log());
        }
    }

    // AC-1.5
    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Escape_cancels_the_pick_and_leaves_no_layer()
    {
        await using var session = await Session.StartAsync(SampleTarget.Desktop);

        Desktop.PressHotkey();
        await Poll.UntilAsync(() => session.App.VisibleWindows().Count > 0, "The pick layer never appeared.", Token);
        // The layer under the pointer takes the keyboard focus on pointer enter.
        Desktop.MoveCursor(session.TargetBounds().Center);
        await Task.Delay(300, Token);
        Desktop.PressEscape();

        var gone = await Poll.HoldsAsync(() => session.App.VisibleWindows().Count == 0, Token, GoneTimeout);
        Assert.True(gone, "A window of the app is still visible after Esc.");
        Assert.DoesNotContain("Attached", session.App.Log());
    }

    // AC-3.1: geometry and visibility only; the overlay is excluded from capture, so the text is a human check.
    [Fact]
    [Trait("Category", "Desktop")]
    public async Task Overlay_covers_the_target_and_is_visible_click_through_and_not_activatable()
    {
        await using var session = await Session.StartAsync(SampleTarget.Desktop);
        await session.AttachAsync();

        var overlay = await session.OverlayAsync();

        var bounds = Desktop.BoundsOf(overlay);
        AssertOver(session.TargetBounds(), bounds);
        Assert.True(bounds.Width > 0 && bounds.Height > 0);
        Assert.True(Desktop.HasExtendedStyles(overlay, Desktop.WsExTransparent | Desktop.WsExLayered | Desktop.WsExNoActivate | Desktop.WsExToolWindow));
        Assert.Equal(byte.MaxValue, Desktop.LayeredAlpha(overlay));
    }

    // AC-3.2
    [Theory]
    [Trait("Category", "Desktop")]
    [InlineData(SampleTarget.Desktop)]
    [InlineData(SampleTarget.Console)]
    public async Task Input_over_the_overlay_reaches_the_target(string targetName)
    {
        await using var session = await Session.StartAsync(targetName);
        await session.AttachAsync();
        await session.OverlayAsync();
        var bounds = session.TargetBounds();

        Assert.Equal(session.Window, Desktop.RootAt((bounds.X + 100, bounds.Y + 100)));
        Assert.Equal(session.Window, Desktop.RootAt(bounds.Center));
        await Desktop.ClickAsync(bounds.Center, Token);

        var foreground = Desktop.ForegroundWindow();
        Assert.Equal(session.Window, Desktop.RootOf(foreground));
        Assert.NotEqual((uint)session.App.Process.Id, Desktop.ProcessOf(foreground));
    }

    // AC-3.3
    [Theory]
    [Trait("Category", "Desktop")]
    [InlineData(SampleTarget.Desktop)]
    [InlineData(SampleTarget.Console)]
    public async Task Overlay_follows_the_target_when_it_moves(string targetName)
    {
        await using var session = await Session.StartAsync(targetName);
        await session.AttachAsync();
        var overlay = await session.OverlayAsync();
        var before = session.TargetBounds();
        var (x, y) = (before.X + 120, before.Y + 80);

        if (targetName == SampleTarget.Desktop)
        {
            Assert.Equal("ok", await session.Target.SendAsync($"move {x} {y}", Token));
        }
        else
        {
            Desktop.MoveTo(session.Window, x, y);
        }

        await Poll.UntilAsync(
            () => session.TargetBounds().X != before.X && Near(Desktop.BoundsOf(overlay), session.TargetBounds()),
            "The overlay did not follow the target.",
            Token,
            FollowTimeout);
    }

    private static void AssertOver(Desktop.Bounds target, Desktop.Bounds overlay) =>
        Assert.True(Near(overlay, target), $"Overlay {overlay} is not over the target {target}.");

    private static bool Near(Desktop.Bounds a, Desktop.Bounds b) =>
        Math.Abs(a.X - b.X) <= TolerancePixels && Math.Abs(a.Y - b.Y) <= TolerancePixels
        && Math.Abs(a.Width - b.Width) <= TolerancePixels && Math.Abs(a.Height - b.Height) <= TolerancePixels;

    /// <summary>A running target in front, and the app waiting for a hotkey.</summary>
    private sealed class Session : IAsyncDisposable
    {
        private Session(SampleTarget target, nint window, AuroraProcess app) => (Target, Window, App) = (target, window, app);

        public SampleTarget Target { get; }

        public nint Window { get; }

        public AuroraProcess App { get; }

        public static async Task<Session> StartAsync(string targetName)
        {
            var target = await SampleTarget.StartAsync(targetName, ["Hello", "World"], Token);
            try
            {
                var window = await target.MainWindowAsync(Token);
                await Desktop.ForegroundAsync(window, Token);
                var app = await AuroraProcess.StartAsync(Token);
                return new Session(target, window, app);
            }
            catch
            {
                await target.DisposeAsync();
                throw;
            }
        }

        public Desktop.Bounds TargetBounds() => Desktop.BoundsOf(Window);

        /// <summary>Hotkey, then a click in the middle of the target once the pick layer is up.</summary>
        public async Task AttachAsync()
        {
            Desktop.PressHotkey();
            await Poll.UntilAsync(() => App.VisibleWindows().Count > 0, "The pick layer never appeared.", Token);
            await Desktop.ClickAsync(TargetBounds().Center, Token);
            await App.WaitForLogAsync("Attached to ", Token);
        }

        public async Task<nint> OverlayAsync()
        {
            await Poll.UntilAsync(() => App.Overlay() is not null, "The overlay never appeared.", Token);
            return App.Overlay()!.Value;
        }

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            await Target.DisposeAsync();
        }
    }
}
