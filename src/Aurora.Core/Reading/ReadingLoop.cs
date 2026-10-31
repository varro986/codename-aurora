using System.Threading.Channels;
using Aurora.Core.Contracts;
using Aurora.Core.Settings;

namespace Aurora.Core.Reading;

public enum ReadingEndKind
{
    TargetClosed,
    OcrLanguageMissing,
    Failed,
}

/// <summary>Why a reading session stopped by itself; Error is set for a missing OCR language or a failure.</summary>
public sealed record ReadingEnd(ReadingEndKind Kind, Exception? Error = null);

/// <summary>
/// One reading session of one target: while the target is in the foreground, frame, change check, OCR, overlay.
/// Single use: <see cref="RunAsync"/> ends when the target closes, the OCR language is missing or the token is cancelled.
/// </summary>
public sealed class ReadingLoop
{
    private readonly Target _target;
    private readonly IFrameSourceFactory _frames;
    private readonly IOcrEngine _ocr;
    private readonly IOverlay _overlay;
    private readonly IWindowEvents _windowEvents;
    private readonly TimeSpan _interval;
    private readonly TimeProvider _time;
    private readonly Channel<TargetEvent> _events = Channel.CreateUnbounded<TargetEvent>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly FrameChange _change = new();

    private IFrameSource? _source;
    private nint _window;
    private PixelRect _bounds;
    private bool _foreground;
    private bool _minimised;
    private IReadOnlyList<OcrLine>? _lines;
    private (int Width, int Height) _frameSize;
    private bool _redraw;
    private DateTimeOffset _nextRead = DateTimeOffset.MinValue;

    public ReadingLoop(
        Target target,
        IFrameSourceFactory frames,
        IOcrEngine ocr,
        IOverlay overlay,
        IWindowEvents windowEvents,
        AuroraSettings settings,
        TimeProvider time)
    {
        _target = target;
        _frames = frames;
        _ocr = ocr;
        _overlay = overlay;
        _windowEvents = windowEvents;
        _interval = settings.ReadingInterval;
        _time = time;
    }

    private bool Active => _foreground && !_minimised;

    public async Task<ReadingEnd> RunAsync(CancellationToken cancellationToken)
    {
        _windowEvents.Changed += OnChanged;
        try
        {
            _windowEvents.Watch(_target);
            return await LoopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _windowEvents.Changed -= OnChanged;
            _windowEvents.Watch(null);
            _overlay.Hide();
            await DisposeSourceAsync().ConfigureAwait(false);
        }
    }

    private void OnChanged(object? sender, TargetEvent e) => _events.Writer.TryWrite(e);

    private async Task<ReadingEnd> LoopAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var end = await DrainAsync().ConfigureAwait(false);
            if (end is null && Active && _time.GetUtcNow() >= _nextRead)
            {
                _nextRead = _time.GetUtcNow() + _interval;
                end = await ReadOnceAsync(cancellationToken).ConfigureAwait(false)
                    ?? await DrainAsync().ConfigureAwait(false);
            }

            if (end is not null)
            {
                return end;
            }

            Redraw();
            await WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ReadingEnd?> DrainAsync()
    {
        while (_events.Reader.TryRead(out var e))
        {
            if (e.Kind == TargetEventKind.Closed)
            {
                return new ReadingEnd(ReadingEndKind.TargetClosed);
            }

            await ApplyAsync(e).ConfigureAwait(false);
        }

        return null;
    }

    private async Task ApplyAsync(TargetEvent e)
    {
        // A window minimised when watched starts with no Gained: its first event says which window this is.
        if (_window == 0)
        {
            _window = e.Window;
        }

        var wasActive = Active;
        switch (e.Kind)
        {
            case TargetEventKind.ForegroundGained:
                if (e.Window != _window)
                {
                    // The lines belong to the previous window: never leave them over the new one.
                    _overlay.Hide();
                    await DisposeSourceAsync().ConfigureAwait(false);
                    _window = e.Window;
                }

                (_foreground, _minimised, _bounds) = (true, false, e.Bounds);
                _nextRead = DateTimeOffset.MinValue;
                break;
            case TargetEventKind.ForegroundLost when e.Window == _window:
                _foreground = false;
                break;
            case TargetEventKind.Minimised when e.Window == _window:
                _minimised = true;
                break;
            case TargetEventKind.Restored when e.Window == _window:
                (_minimised, _bounds) = (false, e.Bounds);
                _nextRead = DateTimeOffset.MinValue;
                break;
            case TargetEventKind.Moved when e.Window == _window:
                _bounds = e.Bounds;
                break;
        }

        if (wasActive && !Active)
        {
            _overlay.Hide();
        }

        _redraw = true;
    }

    private async Task<ReadingEnd?> ReadOnceAsync(CancellationToken cancellationToken)
    {
        _source ??= _target is MonitorTarget monitor
            ? _frames.Create(monitor)
            : _frames.Create(new WindowTarget(_window));

        var frame = await _source.NextAsync(cancellationToken).ConfigureAwait(false);
        if (frame is null && _target is ProcessTarget)
        {
            // One window of the process closed: wait for its next foreground window, or Closed when the process ends.
            _foreground = false;
            _overlay.Hide();
            await DisposeSourceAsync().ConfigureAwait(false);
            return null;
        }

        if (frame is null)
        {
            return new ReadingEnd(ReadingEndKind.TargetClosed);
        }

        if (!_change.Accept(frame))
        {
            return null;
        }

        try
        {
            var result = await _ocr.RecognizeAsync(frame, cancellationToken).ConfigureAwait(false);
            _lines = result.Lines;
            _frameSize = (frame.Width, frame.Height);
            _redraw = true;
            return null;
        }
        catch (OcrLanguageMissingException ex)
        {
            return new ReadingEnd(ReadingEndKind.OcrLanguageMissing, ex);
        }
    }

    private void Redraw()
    {
        if (!_redraw || !Active || _lines is null)
        {
            return;
        }

        _overlay.Show(_bounds, LayoutMapper.Map(_lines, _frameSize.Width, _frameSize.Height, _bounds));
        _redraw = false;
    }

    private async Task WaitAsync(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var due = _nextRead - _time.GetUtcNow();
        var delay = Task.Delay(Active && due > TimeSpan.Zero && due < _interval ? due : _interval, _time, cts.Token);
        var ready = _events.Reader.WaitToReadAsync(cts.Token).AsTask();
        await Task.WhenAny(delay, ready).ConfigureAwait(false);
        await cts.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(delay, ready).ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
    }

    private async Task DisposeSourceAsync()
    {
        if (_source is not null)
        {
            await _source.DisposeAsync().ConfigureAwait(false);
            _source = null;
        }

        _change.Reset();
        _lines = null;
    }
}
