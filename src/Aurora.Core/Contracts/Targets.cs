namespace Aurora.Core.Contracts;

/// <summary>What Aurora is attached to: one at a time.</summary>
public abstract record Target;

public sealed record WindowTarget(nint Handle) : Target;

/// <summary>Every window of the process, including the ones it opens later.</summary>
public sealed record ProcessTarget(int ProcessId) : Target;

public sealed record MonitorTarget(nint Handle) : Target;

/// <summary>An entry of the tray Attach list.</summary>
public sealed record TargetChoice(string Title, Target Target);

/// <summary>Keyboard modifiers; values match the Win32 MOD_* flags so adapters pass them through.</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

public interface ITargetResolver
{
    /// <summary>
    /// The target under the click: the window, or its process or monitor when the configured modifier is held.
    /// Null when nothing can be attached there.
    /// </summary>
    Target? Resolve(PixelPoint click, KeyModifiers modifiers);

    /// <summary>Open windows, their processes and the monitors.</summary>
    IReadOnlyList<TargetChoice> List();
}
