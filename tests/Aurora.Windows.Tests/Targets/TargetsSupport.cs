using System.Runtime.InteropServices;
using Aurora.Core.Contracts;
using Aurora.Windows.Tests.Support;

namespace Aurora.Windows.Tests.Targets;

/// <summary>Collects the events of a watcher and waits for the one a test expects.</summary>
internal sealed class EventLog : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly IWindowEvents _events;
    private readonly List<TargetEvent> _seen = [];

    public EventLog(IWindowEvents events)
    {
        _events = events;
        _events.Changed += OnChanged;
    }

    public IReadOnlyList<TargetEvent> Snapshot()
    {
        lock (_seen)
        {
            return [.. _seen];
        }
    }

    public void Dispose() => _events.Changed -= OnChanged;

    public async Task<TargetEvent> WaitAsync(Func<TargetEvent, bool> match, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + Timeout;
        var from = 0;
        while (DateTime.UtcNow < deadline)
        {
            lock (_seen)
            {
                for (; from < _seen.Count; from++)
                {
                    if (match(_seen[from]))
                    {
                        return _seen[from];
                    }
                }
            }

            await Task.Delay(20, cancellationToken);
        }

        throw new TimeoutException("The expected event did not arrive.");
    }

    private void OnChanged(object? sender, TargetEvent e)
    {
        lock (_seen)
        {
            _seen.Add(e);
        }
    }
}

/// <summary>Win32 calls the tests need, resolved with DllImport because the test project forbids unsafe code.</summary>
internal static class TestNative
{
    private const uint WM_CLOSE = 0x0010;
    private const int PerMonitorAwareV2 = -4;
    private const uint ExtendedFrameBounds = 9;

    // Without it the test process sees virtualised coordinates; set once, before any window is measured.
    static TestNative() => SetProcessDpiAwarenessContext(PerMonitorAwareV2);

    /// <summary>Starts the desktop sample target and waits for its main window.</summary>
    public static async Task<(SampleTarget Target, nint Window)> StartDesktopAsync(CancellationToken cancellationToken)
    {
        var target = await SampleTarget.StartAsync(SampleTarget.Desktop, ["Hello", "World"], cancellationToken);
        return (target, await target.MainWindowAsync(cancellationToken));
    }

    public static ExitedTargetGuard TolerateExit(SampleTarget target) => new(target);

    public static void Close(nint window) => PostMessageW(window, WM_CLOSE, 0, 0);

    public static async Task<nint> FindWindowAsync(string title, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var window = FindWindowW(null, title);
            if (window != 0)
            {
                return window;
            }

            await Task.Delay(50, cancellationToken);
        }

        throw new TimeoutException($"No window titled {title}.");
    }

    /// <summary>The DWM extended frame bounds, as the events report them.</summary>
    public static PixelRect BoundsOf(nint window)
    {
        Marshal.ThrowExceptionForHR(DwmGetWindowAttribute(window, ExtendedFrameBounds, out var rect, Marshal.SizeOf<Rect>()));
        return new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public static PixelRect MonitorBounds(nint monitor)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        Assert.True(GetMonitorInfoW(monitor, ref info));
        var rect = info.Monitor;
        return new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, uint attribute, out Rect value, int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(nint context);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowW(string? className, string windowName);
}

/// <summary>Disposes a target the test already told to exit: closing its broken pipe is expected, not a failure.</summary>
internal sealed class ExitedTargetGuard(SampleTarget target) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        try
        {
            await target.DisposeAsync();
        }
        catch (IOException)
        {
            // The pipe broke because the target exited.
        }
    }
}
