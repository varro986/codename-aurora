using Aurora.Core.Contracts;
using Aurora.Core.Reading;

namespace Aurora.Core.Tests.Reading;

public sealed class ReadingLoopTests : IAsyncDisposable
{
    private const nint Window = 7;
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);
    private static readonly PixelRect Bounds = new(100, 50, 4, 2);

    private readonly FakeFrameSourceFactory _frames = new();
    private readonly FakeOcr _ocr = new();
    private readonly FakeOverlay _overlay = new();
    private readonly FakeWindowEvents _events = new();
    private readonly FakeTimeProvider _time = new();
    private readonly CancellationTokenSource _cts = new();
    private Task<ReadingEnd>? _run;

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        if (_run is not null)
        {
            await _run.ContinueWith(_ => { }, TaskScheduler.Default);
        }

        _cts.Dispose();
    }

    private int TotalReads => _frames.Created.Sum(c => c.Source.Reads);

    private async Task StartAsync(Target target)
    {
        var loop = new ReadingLoop(target, _frames, _ocr, _overlay, _events, Wait.Settings(Interval), _time);
        _run = loop.RunAsync(_cts.Token);
        await Wait.UntilAsync(() => _events.Watched.Count == 1);
    }

    private async Task GainAsync(nint window = Window)
    {
        var before = TotalReads;
        _events.Raise(TargetEventKind.ForegroundGained, window, Bounds);
        await Wait.UntilAsync(() => TotalReads > before);
    }

    private async Task TickAsync()
    {
        var before = TotalReads;
        await Wait.UntilAsync(() => _time.PendingTimers > 0);
        _time.Advance(Interval);
        await Wait.UntilAsync(() => TotalReads > before);
    }

    [Fact]
    public async Task Reads_and_shows_when_the_target_is_in_the_foreground()
    {
        await StartAsync(new WindowTarget(Window));
        await GainAsync();
        await Wait.UntilAsync(() => _overlay.Shown.Count == 1);

        Assert.Equal(new WindowTarget(Window), _frames.Created[0].Target);
        Assert.Equal(Bounds, _overlay.Shown[0].Bounds);
        Assert.Equal(new PixelRect(101, 52, 3, 4), _overlay.Shown[0].Lines[0].Bounds);
    }

    [Fact]
    public async Task Ac_2_1_ocr_runs_only_when_the_content_changes()
    {
        await StartAsync(new WindowTarget(Window));
        await GainAsync();
        await TickAsync();
        await TickAsync();

        Assert.Equal(1, _ocr.Calls);

        _frames.Next = () => Frames.Solid(9);
        await TickAsync();
        await Wait.UntilAsync(() => _ocr.Calls == 2);
    }

    [Fact]
    public async Task Ac_3_4_hides_and_pauses_when_not_in_foreground_then_resumes_without_new_ocr()
    {
        await StartAsync(new WindowTarget(Window));
        await GainAsync();
        await Wait.UntilAsync(() => _overlay.Shown.Count == 1);

        _events.Raise(TargetEventKind.ForegroundLost, Window, Bounds);
        await Wait.UntilAsync(() => _overlay.Hides == 1);
        var reads = TotalReads;
        await Wait.UntilAsync(() => _time.PendingTimers > 0);
        _time.Advance(Interval);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(reads, TotalReads);

        _events.Raise(TargetEventKind.ForegroundGained, Window, Bounds);
        await Wait.UntilAsync(() => _overlay.Shown.Count == 2);
        Assert.Equal(1, _ocr.Calls);
        await TickAsync();
    }

    [Fact]
    public async Task Ac_3_4_hides_when_minimised_and_resumes_when_restored()
    {
        await StartAsync(new WindowTarget(Window));
        await GainAsync();
        await Wait.UntilAsync(() => _overlay.Shown.Count == 1);

        _events.Raise(TargetEventKind.Minimised, Window, Bounds);
        await Wait.UntilAsync(() => _overlay.Hides == 1);

        _events.Raise(TargetEventKind.Restored, Window, Bounds);
        await Wait.UntilAsync(() => _overlay.Shown.Count == 2);
    }

    [Fact]
    public async Task The_first_event_of_a_window_minimised_at_watch_time_identifies_it()
    {
        await StartAsync(new WindowTarget(Window));

        _events.Raise(TargetEventKind.ForegroundLost, Window, Bounds);
        _events.Raise(TargetEventKind.Restored, Window, Bounds);
        await GainAsync();
        await Wait.UntilAsync(() => _overlay.Shown.Count == 1);

        // The window was already known: gaining the foreground is not a switch to another window.
        Assert.Equal(0, _overlay.Hides);
    }

    [Fact]
    public async Task Moved_redraws_the_last_lines_at_the_new_place_without_ocr()
    {
        await StartAsync(new WindowTarget(Window));
        await GainAsync();
        await Wait.UntilAsync(() => _overlay.Shown.Count == 1);

        var moved = new PixelRect(300, 400, 4, 2);
        _events.Raise(TargetEventKind.Moved, Window, moved);
        await Wait.UntilAsync(() => _overlay.Shown.Count == 2);

        Assert.Equal(moved, _overlay.Shown[1].Bounds);
        Assert.Equal(new PixelRect(301, 402, 3, 4), _overlay.Shown[1].Lines[0].Bounds);
        Assert.Equal(1, _ocr.Calls);
    }

    [Fact]
    public async Task Process_target_switches_to_the_window_that_gains_the_foreground()
    {
        await StartAsync(new ProcessTarget(42));
        await GainAsync(Window);
        await GainAsync(8);

        Assert.Equal(new WindowTarget(Window), _frames.Created[0].Target);
        Assert.True(_frames.Created[0].Source.Disposed);
        Assert.Equal(new WindowTarget(8), _frames.Created[1].Target);
    }

    [Fact]
    public async Task Monitor_target_reads_the_monitor()
    {
        await StartAsync(new MonitorTarget(5));
        await GainAsync(5);

        Assert.Equal(new MonitorTarget(5), _frames.Created[0].Target);
    }

    [Fact]
    public async Task Closed_event_ends_the_session_hidden_and_released()
    {
        await StartAsync(new WindowTarget(Window));
        await GainAsync();

        _events.Raise(TargetEventKind.Closed, Window, Bounds);
        var end = await _run!;

        Assert.Equal(ReadingEndKind.TargetClosed, end.Kind);
        Assert.True(_overlay.Hides >= 1);
        Assert.True(_frames.Created[0].Source.Disposed);
        Assert.Null(_events.Watched[^1]);
    }

    [Fact]
    public async Task Source_gone_ends_the_session()
    {
        _frames.Next = () => null;
        await StartAsync(new WindowTarget(Window));
        _events.Raise(TargetEventKind.ForegroundGained, Window, Bounds);

        Assert.Equal(ReadingEndKind.TargetClosed, (await _run!).Kind);
    }

    [Fact]
    public async Task Process_window_gone_waits_for_the_next_window_instead_of_ending()
    {
        _frames.Next = () => null;
        await StartAsync(new ProcessTarget(42));
        await GainAsync(Window);
        await Wait.UntilAsync(() => _frames.Created[0].Source.Disposed);

        _frames.Next = () => Frames.Solid(9);
        await GainAsync(8);

        Assert.False(_run!.IsCompleted);
        Assert.Equal(new WindowTarget(8), _frames.Created[1].Target);
    }

    [Fact]
    public async Task Missing_ocr_language_ends_the_session_with_the_exception()
    {
        var error = new OcrLanguageMissingException("it-IT");
        _ocr.Throw = error;
        await StartAsync(new WindowTarget(Window));
        _events.Raise(TargetEventKind.ForegroundGained, Window, Bounds);

        var end = await _run!;

        Assert.Equal(ReadingEndKind.OcrLanguageMissing, end.Kind);
        Assert.Same(error, end.Error);
    }

    [Fact]
    public async Task Cancellation_stops_hides_and_releases()
    {
        await StartAsync(new WindowTarget(Window));
        await GainAsync();

        await _cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _run!);
        Assert.True(_frames.Created[0].Source.Disposed);
        Assert.True(_overlay.Hides >= 1);
    }
}
