using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Aurora.Core.Contracts;
using Aurora.Windows.Interop;

namespace Aurora.Windows.Targets;

/// <summary>
/// Window events of the watched target through out-of-context WinEvent hooks, which need a message loop: a dedicated
/// thread owns the hooks and runs it. <see cref="Changed"/> is raised on that thread (also for the state raised by
/// <see cref="Watch"/>): handlers must return quickly and marshal to their own thread.
/// Bounds are the DWM extended frame in screen physical pixels, as Windows Graphics Capture sees the window; a
/// minimised window reports the bounds it had before, or its restored rectangle if it was already minimised.
/// Hooks are filtered by the target's process, except the
/// foreground one, which is global because leaving the target is a foreground change of another process.
/// </summary>
public sealed class WindowsWindowEvents : IWindowEvents
{
    private readonly MessageLoopThread _loop;
    private readonly NativeMethods.WinEventProc _callback;
    private readonly nint _callbackPointer;
    private readonly List<nint> _hooks = [];
    private readonly HashSet<nint> _known = [];
    private readonly Dictionary<nint, PixelRect> _bounds = [];
    private int _disposed;

    // State below is touched only on the hook thread.
    private Target? _target;
    private nint _window;
    private uint _processId;
    private Process? _process;
    private int _generation;
    private nint _foreground;
    private bool _hasForeground;

    public WindowsWindowEvents()
    {
        _callback = OnEvent;
        _callbackPointer = Marshal.GetFunctionPointerForDelegate(_callback);
        // Hook callbacks are delivered from inside the loop's GetMessage.
        _loop = new MessageLoopThread("Aurora window events", onMessage: OnMessage);
    }

    public event EventHandler<TargetEvent>? Changed;

    public void Watch(Target? target)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        _loop.Invoke(() => WatchOnThread(target));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _loop.Invoke(Reset);
        }
        catch (ObjectDisposedException)
        {
            // The loop has already ended, and its hooks went with the thread.
        }

        _loop.Dispose();
    }

    private void OnMessage(Interop.NativeMethods.Msg msg)
    {
        if (msg.Message == NativeMethods.WM_PROCESS_EXITED && (int)msg.WParam == _generation)
        {
            Raise(TargetEventKind.Closed, _foreground);
        }
    }

    // A failure leaves nothing behind: no target with half of its hooks.
    private void WatchOnThread(Target? target)
    {
        try
        {
            Start(target);
        }
        catch
        {
            Reset();
            throw;
        }
    }

    private void Start(Target? target)
    {
        Reset();
        _target = target;
        switch (target)
        {
            case MonitorTarget monitor:
                WindowQueries.TryGetMonitorBounds(monitor.Handle, out var bounds);
                Changed?.Invoke(this, new TargetEvent(TargetEventKind.ForegroundGained, monitor.Handle, bounds));
                return;
            case WindowTarget window:
                _window = window.Handle;
                _processId = WindowQueries.ProcessIdOf(window.Handle);
                if (_processId == 0)
                {
                    Raise(TargetEventKind.Closed, _window);
                    return;
                }

                break;
            case ProcessTarget process:
                _processId = (uint)process.ProcessId;
                if (!TryWatchProcessExit(process.ProcessId))
                {
                    Raise(TargetEventKind.Closed, 0);
                    return;
                }

                _known.UnionWith(WindowQueries.AttachableWindows().Where(w => WindowQueries.ProcessIdOf(w) == _processId));
                break;
            default:
                return;
        }

        Hook(NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND, 0);
        Hook(NativeMethods.EVENT_SYSTEM_MOVESIZEEND, NativeMethods.EVENT_SYSTEM_MOVESIZEEND, _processId);
        Hook(NativeMethods.EVENT_SYSTEM_MINIMIZESTART, NativeMethods.EVENT_SYSTEM_MINIMIZEEND, _processId);
        Hook(NativeMethods.EVENT_OBJECT_LOCATIONCHANGE, NativeMethods.EVENT_OBJECT_LOCATIONCHANGE, _processId);
        Hook(NativeMethods.EVENT_OBJECT_DESTROY, NativeMethods.EVENT_OBJECT_DESTROY, _processId);
        if (_target is ProcessTarget)
        {
            Hook(NativeMethods.EVENT_OBJECT_SHOW, NativeMethods.EVENT_OBJECT_SHOW, _processId);
        }

        RaiseCurrentState();
    }

    private void Reset()
    {
        foreach (var hook in _hooks)
        {
            NativeMethods.UnhookWinEvent(hook);
        }

        _hooks.Clear();
        _known.Clear();
        _bounds.Clear();
        _process?.Dispose();
        _process = null;
        _generation++;
        (_target, _window, _processId, _foreground, _hasForeground) = (null, 0, 0, 0, false);
    }

    private void Hook(uint min, uint max, uint processId)
    {
        var hook = NativeMethods.SetWinEventHook(min, max, 0, _callbackPointer, processId, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
        if (hook == 0)
        {
            throw new Win32Exception();
        }

        _hooks.Add(hook);
    }

    // The process end is awaited on its handle; the exit is posted back so Closed is raised on the hook thread.
    private bool TryWatchProcessExit(int processId)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return false;
        }

        try
        {
            var generation = _generation;
            process.Exited += (_, _) => _loop.Post(NativeMethods.WM_PROCESS_EXITED, (nuint)generation);
            process.EnableRaisingEvents = true;
        }
        catch (Win32Exception ex)
        {
            process.Dispose();
            throw new InvalidOperationException($"The process {processId} cannot be watched (it may run elevated).", ex);
        }

        _process = process;
        return true;
    }

    private void RaiseCurrentState()
    {
        var foreground = WindowQueries.RootOf(NativeMethods.GetForegroundWindow());
        if (foreground != 0 && Matches(foreground))
        {
            OnForeground(foreground);
            return;
        }

        _foreground = _target is WindowTarget ? _window : _known.FirstOrDefault();
        Raise(TargetEventKind.ForegroundLost, _foreground);
    }

    // Called by Windows: an exception must not cross back into it, so it is rethrown on the thread pool.
    private void OnEvent(nint hook, uint eventType, nint window, int objectId, int childId, uint thread, uint time)
    {
        try
        {
            Dispatch(eventType, window, objectId, childId);
        }
        catch (Exception ex)
        {
            MessageLoopThread.Escalate(ex);
        }
    }

    private void Dispatch(uint eventType, nint window, int objectId, int childId)
    {
        if (objectId != NativeMethods.OBJID_WINDOW || childId != 0 || window == 0 || _target is null)
        {
            return;
        }

        switch (eventType)
        {
            case NativeMethods.EVENT_SYSTEM_FOREGROUND:
                OnForeground(WindowQueries.RootOf(window));
                break;
            case NativeMethods.EVENT_OBJECT_DESTROY:
                OnDestroyed(window);
                break;
            case NativeMethods.EVENT_OBJECT_SHOW:
                OnShown(window);
                break;
            case NativeMethods.EVENT_OBJECT_LOCATIONCHANGE or NativeMethods.EVENT_SYSTEM_MOVESIZEEND:
                OnMoved(window);
                break;
            case NativeMethods.EVENT_SYSTEM_MINIMIZESTART when Matches(window):
                Raise(TargetEventKind.Minimised, window);
                break;
            case NativeMethods.EVENT_SYSTEM_MINIMIZEEND when Matches(window):
                Raise(TargetEventKind.Restored, window);
                break;
        }
    }

    private bool Matches(nint window) => _target switch
    {
        WindowTarget => window == _window,
        ProcessTarget => WindowQueries.ProcessIdOf(window) == _processId
            && WindowQueries.RootOf(window) == window
            && WindowQueries.IsAttachable(window),
        _ => false,
    };

    private void OnForeground(nint window)
    {
        if (window != 0 && Matches(window))
        {
            _known.Add(window);
            if (!_hasForeground || _foreground != window)
            {
                (_foreground, _hasForeground) = (window, true);
                Raise(TargetEventKind.ForegroundGained, window);
            }
        }
        else if (_hasForeground)
        {
            _hasForeground = false;
            Raise(TargetEventKind.ForegroundLost, _foreground);
        }
    }

    private void OnDestroyed(nint window)
    {
        if (_target is WindowTarget)
        {
            if (window == _window)
            {
                Raise(TargetEventKind.Closed, window);
            }

            return;
        }

        if (!_known.Remove(window))
        {
            return;
        }

        if (window == _foreground && _hasForeground)
        {
            // The system activates the next window of the process; take it now if it already is the foreground one.
            _hasForeground = false;
            var next = WindowQueries.RootOf(NativeMethods.GetForegroundWindow());
            if (next != window)
            {
                OnForeground(next);
            }
        }

        _bounds.Remove(window);
    }

    private void OnShown(nint window)
    {
        if (Matches(window) && _known.Add(window))
        {
            Raise(TargetEventKind.WindowAdded, window);
        }
    }

    private void OnMoved(nint window)
    {
        if (!Matches(window))
        {
            return;
        }

        _bounds.TryGetValue(window, out var before);
        var bounds = BoundsOf(window);
        if (bounds != before)
        {
            Changed?.Invoke(this, new TargetEvent(TargetEventKind.Moved, window, bounds));
        }
    }

    private void Raise(TargetEventKind kind, nint window) =>
        Changed?.Invoke(this, new TargetEvent(kind, window, BoundsOf(window)));

    // Minimised windows sit off-screen and destroyed ones have none: the last known bounds stand in, or the
    // restored rectangle of a window that was already minimised when first seen.
    private PixelRect BoundsOf(nint window)
    {
        if (window == 0)
        {
            return default;
        }

        if (!NativeMethods.IsIconic(window) && WindowQueries.TryGetBounds(window, out var bounds))
        {
            return _bounds[window] = bounds;
        }

        if (_bounds.TryGetValue(window, out var known))
        {
            return known;
        }

        return WindowQueries.TryGetRestoredBounds(window, out var restored) ? _bounds[window] = restored : default;
    }
}
