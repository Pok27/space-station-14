using Robust.Shared.Prototypes;

namespace Content.Shared.StatusEffectNew.Components;

/// <summary>
/// Applies a set of permanent status effects while this component exists.
/// </summary>
[RegisterComponent]
public sealed partial class PermanentStatusEffectsComponent : Component
{
    [DataField(required: true)]
    public HashSet<EntProtoId> StatusEffects = new();
}
