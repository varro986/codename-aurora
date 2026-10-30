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

    /// <summary>Watches one target, replacing the previous one; null stops watching.</summary>
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
