using Aurora.Core.Contracts;
using Aurora.Core.Reading;

namespace Aurora.Core.Tests.Reading;

public sealed class AttachmentTests : IAsyncDisposable
{
    private static readonly PixelRect Bounds = new(0, 0, 4, 2);

    private readonly FakeFrameSourceFactory _frames = new();
    private readonly FakeOverlay _overlay = new();
    private readonly FakeWindowEvents _events = new();
    private readonly Attachment _attachment;
    private readonly List<ReadingEnd> _detached = [];

    public AttachmentTests()
    {
        _attachment = new Attachment(
            _frames, new FakeOcr(), _overlay, _events, Wait.Settings(TimeSpan.FromMilliseconds(500)), new FakeTimeProvider());
        _attachment.Detached += (_, end) => _detached.Add(end);
    }

    public ValueTask DisposeAsync() => _attachment.DisposeAsync();

    [Fact]
    public async Task Ac_1_6_attaching_again_stops_the_previous_session_and_watches_the_new_target()
    {
        var first = new WindowTarget(1);
        var second = new MonitorTarget(2);
        await _attachment.AttachAsync(first);
        await Wait.UntilAsync(() => _events.Watched.Count == 1);
        _events.Raise(TargetEventKind.ForegroundGained, 1, Bounds);
        await Wait.UntilAsync(() => _frames.Created.Count == 1);

        await _attachment.AttachAsync(second);

        Assert.True(_frames.Created[0].Source.Disposed);
        Assert.Equal(second, _attachment.Target);
        await Wait.UntilAsync(() => _events.Watched.Count == 3);
        Assert.Equal([first, null, second], _events.Watched);
        Assert.Empty(_detached);
    }

    [Fact]
    public async Task Ac_2_5_detaches_by_itself_when_the_target_closes()
    {
        await _attachment.AttachAsync(new WindowTarget(1));
        await Wait.UntilAsync(() => _events.Watched.Count == 1);

        _events.Raise(TargetEventKind.Closed, 1, Bounds);
        await Wait.UntilAsync(() => _detached.Count == 1);

        Assert.Equal(ReadingEndKind.TargetClosed, _detached[0].Kind);
        Assert.Null(_attachment.Target);
        Assert.True(_overlay.Hides >= 1);
    }

    [Fact]
    public async Task Detach_stops_without_raising_the_event()
    {
        await _attachment.AttachAsync(new WindowTarget(1));
        await Wait.UntilAsync(() => _events.Watched.Count == 1);

        await _attachment.DetachAsync();

        Assert.Null(_attachment.Target);
        Assert.Null(_events.Watched[^1]);
        Assert.True(_overlay.Hides >= 1);
        Assert.Empty(_detached);
    }

    [Fact]
    public async Task A_failing_session_is_reported_not_lost()
    {
        var error = new InvalidOperationException("boom");
        _frames.Next = () => throw error;
        await _attachment.AttachAsync(new WindowTarget(1));
        await Wait.UntilAsync(() => _events.Watched.Count == 1);

        _events.Raise(TargetEventKind.ForegroundGained, 1, Bounds);
        await Wait.UntilAsync(() => _detached.Count == 1);

        Assert.Equal(ReadingEndKind.Failed, _detached[0].Kind);
        Assert.Same(error, _detached[0].Error);
    }
}
