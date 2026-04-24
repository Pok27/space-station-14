namespace Content.Shared.Revenant.Components;

/// <summary>
/// Makes the target solid, visible, and applies a slowdown.
/// Use only in conjunction with the new status effect system, on the status effect entity.
/// </summary>
[RegisterComponent]
public sealed partial class CorporealStatusEffectComponent : Component
{
    /// <summary>
    /// The movement speed multiplier applied while the effect is active.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float MovementSpeedDebuff = 0.66f;
}
