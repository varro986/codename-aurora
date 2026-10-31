using System.Diagnostics;
using System.Numerics;
using Aurora.Core.Contracts;

namespace Aurora.Windows.Targets;

/// <summary>
/// Resolves clicks and lists targets. A click is resolved to the attachable top-level window under it (windows of
/// Aurora itself are looked through); the desktop shell and untitled helper windows are not attachable, so a click
/// there gives null unless the monitor modifier is held. When both modifiers are held the one with more keys wins.
/// </summary>
public sealed class WindowsTargetResolver(KeyModifiers processModifier, KeyModifiers monitorModifier) : ITargetResolver
{
    public Target? Resolve(PixelPoint click, KeyModifiers modifiers)
    {
        var point = new NativeMethods.Point { X = click.X, Y = click.Y };
        var monitorHeld = IsHeld(monitorModifier, modifiers);
        var processHeld = IsHeld(processModifier, modifiers);
        if (monitorHeld && (!processHeld || BitOperations.PopCount((uint)monitorModifier) > BitOperations.PopCount((uint)processModifier)))
        {
            var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONULL);
            return monitor == 0 ? null : new MonitorTarget(monitor);
        }

        var window = WindowAt(point, click);
        if (window == 0)
        {
            return null;
        }

        return processHeld ? new ProcessTarget((int)WindowQueries.ProcessIdOf(window)) : new WindowTarget(window);
    }

    public IReadOnlyList<TargetChoice> List()
    {
        var choices = new List<TargetChoice>();
        var processes = new HashSet<uint>();
        var windows = WindowQueries.AttachableWindows();
        foreach (var window in windows)
        {
            choices.Add(new TargetChoice(WindowQueries.TitleOf(window), new WindowTarget(window)));
        }

        foreach (var processId in windows.Select(WindowQueries.ProcessIdOf).Where(processes.Add))
        {
            choices.Add(new TargetChoice(AllWindowsOf(processId), new ProcessTarget((int)processId)));
        }

        var number = 0;
        foreach (var (handle, bounds) in WindowQueries.Monitors())
        {
            number++;
            choices.Add(new TargetChoice(MonitorName(number, bounds), new MonitorTarget(handle)));
        }

        return choices;
    }

    public string Describe(Target target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target switch
        {
            WindowTarget window => WindowQueries.TitleOf(window.Handle) is { Length: > 0 } title ? title : "Window",
            ProcessTarget process => AllWindowsOf((uint)process.ProcessId),
            MonitorTarget monitor => DescribeMonitor(monitor.Handle),
            _ => throw new ArgumentException($"Unknown target type {target.GetType().Name}.", nameof(target)),
        };
    }

    private static bool IsHeld(KeyModifiers required, KeyModifiers held) =>
        required != KeyModifiers.None && (held & required) == required;

    // WindowFromPoint is exact (honours transparent hit-testing) but returns Aurora's own pick layer when it is
    // open: then the first attachable window under the point in z-order is used.
    private static nint WindowAt(NativeMethods.Point point, PixelPoint click)
    {
        var root = WindowQueries.RootOf(NativeMethods.WindowFromPoint(point));
        if (root != 0 && !WindowQueries.IsOwn(root))
        {
            return WindowQueries.IsAttachable(root) ? root : 0;
        }

        return WindowQueries.AttachableWindows().FirstOrDefault(window =>
            !NativeMethods.IsIconic(window) && WindowQueries.TryGetBounds(window, out var bounds) && Contains(bounds, click));
    }

    private static bool Contains(PixelRect bounds, PixelPoint point) =>
        point.X >= bounds.X && point.X < bounds.Right && point.Y >= bounds.Y && point.Y < bounds.Bottom;

    private static string MonitorName(int number, PixelRect bounds) => $"Monitor {number} ({bounds.Width}x{bounds.Height})";

    private static string DescribeMonitor(nint handle)
    {
        var monitors = WindowQueries.Monitors();
        var index = monitors.FindIndex(m => m.Handle == handle);
        return index < 0 ? "Monitor" : MonitorName(index + 1, monitors[index].Bounds);
    }

    private static string AllWindowsOf(uint processId) => $"All windows of {ProcessName(processId)}";

    private static string ProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return $"Process {processId}";
        }
    }
}
