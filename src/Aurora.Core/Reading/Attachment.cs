using Aurora.Core.Contracts;
using Aurora.Core.Settings;

namespace Aurora.Core.Reading;

/// <summary>Holds at most one target and the reading session that serves it.</summary>
public sealed class Attachment : IAsyncDisposable
{
    private readonly IFrameSourceFactory _frames;
    private readonly IOcrEngine _ocr;
    private readonly IOverlay _overlay;
    private readonly IWindowEvents _windowEvents;
    private readonly AuroraSettings _settings;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();
    private Session? _current;
    private int _disposed;

    public Attachment(
        IFrameSourceFactory frames,
        IOcrEngine ocr,
        IOverlay overlay,
        IWindowEvents windowEvents,
        AuroraSettings settings,
        TimeProvider time)
    {
        _frames = frames;
        _ocr = ocr;
        _overlay = overlay;
        _windowEvents = windowEvents;
        _settings = settings;
        _time = time;
    }

    /// <summary>
    /// Raised on a thread-pool thread when the session ends by itself (target closed, OCR language missing, failure);
    /// not on <see cref="DetachAsync"/>.
    /// </summary>
    public event EventHandler<ReadingEnd>? Detached;

    public Target? Target
    {
        get
        {
            lock (_sync)
            {
                return _current?.Target;
            }
        }
    }

    /// <summary>Attaches to the target, first stopping the previous session.</summary>
    public async Task AttachAsync(Target target)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopAsync().ConfigureAwait(false);
            var session = new Session(target, new ReadingLoop(target, _frames, _ocr, _overlay, _windowEvents, _settings, _time));
            lock (_sync)
            {
                _current = session;
            }

            session.Run = RunAsync(session);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DetachAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await DetachAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private async Task StopAsync()
    {
        Session? session;
        lock (_sync)
        {
            session = _current;
            _current = null;
        }

        if (session is null)
        {
            return;
        }

        await session.Cancel.CancelAsync().ConfigureAwait(false);
        await session.Run!.ConfigureAwait(false);
        session.Cancel.Dispose();
    }

    private async Task RunAsync(Session session)
    {
        ReadingEnd end;
        try
        {
            end = await session.Loop.RunAsync(session.Cancel.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (session.Cancel.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            end = new ReadingEnd(ReadingEndKind.Failed, ex);
        }

        lock (_sync)
        {
            if (_current != session)
            {
                return;
            }

            _current = null;
        }

        session.Cancel.Dispose();
        Detached?.Invoke(this, end);
    }

    private sealed class Session(Target target, ReadingLoop loop)
    {
        public Target Target { get; } = target;

        public ReadingLoop Loop { get; } = loop;

        public CancellationTokenSource Cancel { get; } = new();

        public Task? Run { get; set; }
    }
}
