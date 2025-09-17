using Content.Shared.Damage;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomDamage : SymptomBehavior
{
    /// <summary>
    /// Damage to apply across one or more types.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage { get; private set; } = new();
}

public sealed partial class DiseaseSymptomSystem
{
    /// <summary>
    /// Applies configured damage to the carrier.
    /// </summary>
    private void DoDamage(Entity<DiseaseCarrierComponent> ent, SymptomDamage dmg)
    {
        if (dmg.Damage == null || dmg.Damage.Empty)
            return;

        _damageable.TryChangeDamage(ent.Owner, new DamageSpecifier(dmg.Damage));
    }
}


