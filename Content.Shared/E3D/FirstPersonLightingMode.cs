namespace Content.Shared.E3D;

/// <summary>
/// High-level lighting mode for pseudo-3D rendering.
/// </summary>
public enum FirstPersonLightingMode : byte
{
    Unlit = 0,
    Ambient,
    DistanceFog,
}
