using Aurora.Core.Contracts;
using Aurora.Core.Settings;

namespace Aurora.Core.Tests.Reading;

internal static class Wait
{
    public static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the condition.");
            await Task.Delay(2, TestContext.Current.CancellationToken);
        }
    }

    public static AuroraSettings Settings(TimeSpan interval) => new(
        new HotkeyGesture(KeyModifiers.Control, "A"), KeyModifiers.Control, KeyModifiers.Shift, interval, null, @"C:\Logs\aurora.log", 1024 * 1024);
}

internal static class Frames
{
    public static Frame Solid(byte value, int width = 4, int height = 2) =>
        new(width, height, width * 4, Enumerable.Repeat(value, width * height * 4).ToArray());
}

/// <summary>A time provider whose timers fire only when the test advances it.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly List<FakeTimer> _timers = [];
    private DateTimeOffset _now = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Timers waiting to fire: a test advances time only once the loop waits, or the tick would be lost.</summary>
    public int PendingTimers
    {
        get
        {
            lock (_timers)
            {
                return _timers.Count;
            }
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state, dueTime);
        lock (_timers)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    public void Advance(TimeSpan by)
    {
        FakeTimer[] due;
        lock (_timers)
        {
            _now += by;
            due = _timers.Where(t => t.Due <= _now).ToArray();
            foreach (var timer in due)
            {
                _timers.Remove(timer);
            }
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    private void Remove(FakeTimer timer)
    {
        lock (_timers)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class FakeTimer : ITimer
    {
        private readonly FakeTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;

        public FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state, TimeSpan dueTime)
        {
            (_owner, _callback, _state) = (owner, callback, state);
            Due = owner._now + dueTime;
        }

        public DateTimeOffset Due { get; }

        public void Fire() => _callback(_state);

        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose() => _owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class FakeFrameSource(Func<Frame?> next) : IFrameSource
{
    public int Reads { get; private set; }

    public bool Disposed { get; private set; }

    public Task<Frame?> NextAsync(CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(next());
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeFrameSourceFactory : IFrameSourceFactory
{
    public Func<Frame?> Next { get; set; } = () => Frames.Solid(1);

    public List<(Target Target, FakeFrameSource Source)> Created { get; } = [];

    public IFrameSource Create(WindowTarget window) => Make(window);

    public IFrameSource Create(MonitorTarget monitor) => Make(monitor);

    private FakeFrameSource Make(Target target)
    {
        var source = new FakeFrameSource(() => Next());
        Created.Add((target, source));
        return source;
    }
}

internal sealed class FakeOcr : IOcrEngine
{
    public int Calls { get; private set; }

    public Exception? Throw { get; set; }

    public IReadOnlyList<OcrLine> Lines { get; set; } = [new OcrLine("hello", new PixelRect(1, 2, 3, 4))];

    public Task<OcrResult> RecognizeAsync(Frame frame, CancellationToken cancellationToken)
    {
        Calls++;
        return Throw is null ? Task.FromResult(new OcrResult(Lines)) : Task.FromException<OcrResult>(Throw);
    }
}

internal sealed class FakeOverlay : IOverlay
{
    public List<(PixelRect Bounds, IReadOnlyList<OcrLine> Lines)> Shown { get; } = [];

    public int Hides { get; private set; }

    public void Show(PixelRect targetBounds, IReadOnlyList<OcrLine> lines) => Shown.Add((targetBounds, lines));

    public void Hide() => Hides++;
}

internal sealed class FakeWindowEvents : IWindowEvents
{
    public event EventHandler<TargetEvent>? Changed;

    public List<Target?> Watched { get; } = [];

    public void Watch(Target? target) => Watched.Add(target);

    public void Raise(TargetEventKind kind, nint window, PixelRect bounds) =>
        Changed?.Invoke(this, new TargetEvent(kind, window, bounds));

    public void Dispose()
    {
    }
}
