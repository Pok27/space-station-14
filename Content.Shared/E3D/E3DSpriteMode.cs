namespace Content.Shared.E3D;

/// <summary>
/// Controls how a pseudo-3D renderable resolves its source sprite.
/// </summary>
public enum E3DSpriteMode : byte
{
    Auto = 0,
    Billboard,
    Static,
    Directional,
    WallFace,
}
