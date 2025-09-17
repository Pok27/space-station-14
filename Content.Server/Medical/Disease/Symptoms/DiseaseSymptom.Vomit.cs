using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomVomit : SymptomBehavior
{
}

public sealed partial class DiseaseSymptomSystem
{
    /// <summary>
    /// Forces the carrier to vomit. Used by food poisoning and similar symptoms.
    /// </summary>
    private void DoVomit(Entity<DiseaseCarrierComponent> ent, SymptomVomit vomit)
    {
        _vomit.Vomit(ent, force: true);
    }
}
