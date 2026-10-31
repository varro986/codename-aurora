using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Aurora.Windows.Interop;

/// <summary>
/// A dedicated thread that runs a Win32 message loop, for what must live on one thread (hotkeys, WinEvent hooks).
/// Work is queued with <see cref="Invoke{T}"/> and completes with the loop; work still pending when the loop ends
/// fails with <see cref="ObjectDisposedException"/>, so a caller never waits for a loop that is gone.
/// </summary>
internal sealed class MessageLoopThread : IDisposable
{
    private readonly string _name;
    private readonly Action? _onStart;
    private readonly Action<NativeMethods.Msg>? _onMessage;
    private readonly Action? _onStop;
    private readonly ConcurrentQueue<Work> _work = new();
    private readonly Thread _thread;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private uint _threadId;
    private int _stopped;

    /// <param name="name">Thread name, also the object name of the exceptions of a stopped loop.</param>
    /// <param name="onStart">Runs on the loop thread before the loop; a failure fails the constructor.</param>
    /// <param name="onMessage">Runs on the loop thread for every message but the work wake-up.</param>
    /// <param name="onStop">Runs on the loop thread when the loop ends.</param>
    public MessageLoopThread(string name, Action? onStart = null, Action<NativeMethods.Msg>? onMessage = null, Action? onStop = null)
    {
        (_name, _onStart, _onMessage, _onStop) = (name, onStart, onMessage, onStop);
        _thread = new Thread(Run) { IsBackground = true, Name = name };
        _thread.Start();
        try
        {
            _ready.Task.GetAwaiter().GetResult();
        }
        catch
        {
            _thread.Join();
            throw;
        }
    }

    public uint ThreadId => _threadId;

    public bool HasStopped => Volatile.Read(ref _stopped) != 0;

    /// <summary>An exception that must not cross a native boundary: rethrown on the thread pool, so it is never lost.</summary>
    public static void Escalate(Exception error) =>
        ThreadPool.QueueUserWorkItem(_ => ExceptionDispatchInfo.Capture(error).Throw());

    /// <summary>Runs the action on the loop thread and returns its result; inline when already on that thread.</summary>
    public T Invoke<T>(Func<T> action)
    {
        if (OnLoopThread)
        {
            return action();
        }

        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Enqueue(new Work(
            () =>
            {
                try
                {
                    done.SetResult(action());
                }
                catch (Exception ex)
                {
                    done.SetException(ex);
                }
            },
            error => done.TrySetException(error)));

        // Enqueue first, then look: either the loop's final drain sees the item or this call fails it, never neither.
        if (HasStopped)
        {
            FailPending(new ObjectDisposedException(_name));
        }
        else if (!NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_APP, 0, 0))
        {
            FailPending(new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        return done.Task.GetAwaiter().GetResult();
    }

    public void Invoke(Action action) =>
        Invoke(() =>
        {
            action();
            return true;
        });

    /// <summary>Posts a message to the loop from any thread; false when the loop is gone.</summary>
    public bool Post(uint message, nuint wParam) => NativeMethods.PostThreadMessage(_threadId, message, wParam, 0);

    /// <summary>Ends the loop and waits for the thread; does nothing when called on the loop thread or twice.</summary>
    public void Dispose()
    {
        if (OnLoopThread)
        {
            return;
        }

        try
        {
            Invoke(() => NativeMethods.PostQuitMessage(0));
        }
        catch (ObjectDisposedException)
        {
            // The loop has already ended: only the thread is left to wait for.
        }

        _thread.Join();
    }

    private bool OnLoopThread => Environment.CurrentManagedThreadId == _thread.ManagedThreadId;

    private void Run()
    {
        try
        {
            // Peeking creates the thread's message queue, so posted messages are not lost before GetMessage.
            NativeMethods.PeekMessage(out _, 0, 0, 0, 0);
            _threadId = NativeMethods.GetCurrentThreadId();
            _onStart?.Invoke();
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _stopped, 1);
            _ready.SetException(ex);
            return;
        }

        _ready.SetResult();
        try
        {
            while (NativeMethods.GetMessage(out var msg, 0, 0, 0) > 0)
            {
                if (msg.Message == NativeMethods.WM_APP)
                {
                    RunPending();
                    continue;
                }

                try
                {
                    _onMessage?.Invoke(msg);
                }
                catch (Exception ex)
                {
                    Escalate(ex);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _stopped, 1);
            FailPending(new ObjectDisposedException(_name));
            try
            {
                _onStop?.Invoke();
            }
            catch (Exception ex)
            {
                Escalate(ex);
            }
        }
    }

    private void RunPending()
    {
        while (_work.TryDequeue(out var work))
        {
            work.Run();
        }
    }

    private void FailPending(Exception error)
    {
        while (_work.TryDequeue(out var work))
        {
            work.Fail(error);
        }
    }

    private sealed record Work(Action Run, Action<Exception> Fail);
}
