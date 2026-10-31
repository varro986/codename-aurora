using Aurora.Core.Contracts;
using Aurora.Windows.Targets;

namespace Aurora.Windows.Capture;

/// <param name="pollInterval">How often a source still waiting for its first frame checks that its target exists.</param>
public sealed class WindowsFrameSourceFactory(TimeSpan pollInterval) : IFrameSourceFactory
{
    public IFrameSource Create(WindowTarget window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new WindowsFrameSource(CaptureItemFactory.ForWindow, Interop.NativeMethods.IsWindow, window.Handle, pollInterval);
    }

    public IFrameSource Create(MonitorTarget monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return new WindowsFrameSource(CaptureItemFactory.ForMonitor, IsPresent, monitor.Handle, pollInterval);
    }

    private static bool IsPresent(nint monitor) => WindowQueries.TryGetMonitorBounds(monitor, out _);
}
