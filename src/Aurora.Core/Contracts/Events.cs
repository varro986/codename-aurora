namespace Aurora.Core.Contracts;

public enum TargetEventKind
{
    ForegroundGained,
    ForegroundLost,
    Moved,
    Minimised,
    Restored,
    WindowAdded,
    Closed,
}

/// <summary>A change of the watched target; Window is the window it concerns, Bounds its screen rectangle.</summary>
public sealed record TargetEvent(TargetEventKind Kind, nint Window, PixelRect Bounds);

public interface IWindowEvents : IDisposable
{
    event EventHandler<TargetEvent>? Changed;

    /// <summary>
    /// Watches one target, replacing the previous one; null stops watching. The current state is raised at once
    /// (ForegroundGained or ForegroundLost with the window and its bounds; a monitor raises ForegroundGained with its
    /// own handle and bounds), then every change. For a process, every event carries the process window it concerns,
    /// ForegroundGained comes for whichever of its windows takes the foreground, and Closed only when the process ends.
    /// </summary>
    void Watch(Target? target);
}

/// <summary>A global hotkey: modifiers plus a key name ("A", "F8").</summary>
public sealed record HotkeyGesture(KeyModifiers Modifiers, string Key);

public interface IHotkey : IDisposable
{
    event EventHandler? Pressed;

    /// <summary>False when another application already holds the gesture.</summary>
    bool TryRegister(HotkeyGesture gesture);
}
