using Content.Shared.Damage;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

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
