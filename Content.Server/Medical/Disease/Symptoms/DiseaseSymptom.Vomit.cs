using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomVomit : SymptomBehavior
{
}

public sealed partial class SymptomVomit
{
    [Dependency] private readonly VomitSystem _vomitSystem = default!;

    /// <summary>
    /// Forces the carrier to vomit. Used by food poisoning and similar symptoms.
    /// </summary>
    public override void OnSymptom(EntityUid uid, DiseasePrototype disease)
    {
        _vomitSystem.Vomit(uid, force: true);
    }
}
