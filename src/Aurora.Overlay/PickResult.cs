using Aurora.Core.Contracts;

namespace Aurora.Overlay;

/// <summary>Where the user clicked, in physical screen pixels, and the modifiers held.</summary>
public sealed record PickResult(PixelPoint Point, KeyModifiers Modifiers);
