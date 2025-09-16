using Content.Shared.Damage;

namespace Content.Shared.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomDamage : SymptomBehavior
{
    /// <summary>
    /// Damage to apply across one or more types.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage { get; private set; } = new();
}
