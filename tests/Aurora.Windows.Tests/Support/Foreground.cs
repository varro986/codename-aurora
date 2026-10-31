using System.Runtime.InteropServices;

namespace Aurora.Windows.Tests.Support;

/// <summary>Brings a window to the foreground for desktop tests; shared by the adapter and end-to-end tests.</summary>
public static partial class Foreground
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Retry = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Windows refuses SetForegroundWindow to a process that did not receive the last input, so the call joins the
    /// input queue of the current foreground thread first, and retries until the window is really in front.
    /// </summary>
    public static async Task BringAsync(nint window, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (GetForegroundWindow() != window)
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("The window did not come to the foreground.");
            }

            Request(window);
            await Task.Delay(Retry, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void Request(nint window)
    {
        var current = GetCurrentThreadId();
        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var joined = foregroundThread != current && AttachThreadInput(current, foregroundThread, true);
        try
        {
            BringWindowToTop(window);
            SetForegroundWindow(window);
        }
        finally
        {
            if (joined)
            {
                AttachThreadInput(current, foregroundThread, false);
            }
        }
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BringWindowToTop(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint thread, uint other, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);
}
