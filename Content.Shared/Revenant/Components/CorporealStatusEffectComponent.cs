using Content.Shared.Physics;

namespace Content.Shared.Revenant.Components;

/// <summary>
/// Makes the target solid and visible.
/// Use only in conjunction with the new status effect system, on the status effect entity.
/// </summary>
[RegisterComponent]
public sealed partial class CorporealStatusEffectComponent : Component
{
    /// <summary>
    /// The collision mask applied while the target is corporeal.
    /// </summary>
    [DataField]
    public int CollisionMask = (int)(CollisionGroup.SmallMobMask | CollisionGroup.GhostImpassable);

    /// <summary>
    /// The collision layer applied while the target is corporeal.
    /// </summary>
    [DataField]
    public int CollisionLayer = (int)CollisionGroup.SmallMobLayer;
}
