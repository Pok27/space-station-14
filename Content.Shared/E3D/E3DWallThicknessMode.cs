namespace Content.Shared.E3D;

/// <summary>
/// Controls how thick a wall-like renderable should be treated during pseudo-3D projection.
/// </summary>
public enum E3DWallThicknessMode : byte
{
    Auto = 0,
    Thin,
    FullTile,
}
