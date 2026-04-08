namespace Content.Shared.E3D;

/// <summary>
/// High-level render archetypes for pseudo-3D first-person view.
/// This is the SS14 equivalent of Yog/TG e3D's E3D_TYPE_* tags, but expressed as content data.
/// </summary>
public enum E3DArchetype : byte
{
    /// <summary>
    /// Default: render as a camera-facing sprite (billboard).
    /// </summary>
    Billboard = 0,

    Floor,
    Wall,
    SmoothWall,
    Edge,
    Door,
    Window,
    Wallmount,
    Table,
    Item,
    GasOverlay,
    Mob,
    Frame,
    OccluderOnly,
    DecalLike,
}

