using Content.Shared.FixedPoint;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class CureReagent : CureStep
{
    /// <summary>
    /// Reagent prototype ID to consume.
    /// </summary>
    [DataField(required: true)]
    public string ReagentId { get; private set; } = string.Empty;

    /// <summary>
    /// Required reagent quantity in units.
    /// </summary>
    [DataField]
    public FixedPoint2 Quantity { get; private set; } = FixedPoint2.New(1);
}

public sealed partial class DiseaseCureSystem
{
    /// <summary>
    /// Consumes the specified reagent from the carrier's solutions to cure the disease.
    /// </summary>
    private bool DoCureReagent(Entity<DiseaseCarrierComponent> ent, CureReagent reagentStep, DiseasePrototype disease)
    {
        var reagent = _solutionSystem.TryRemoveReagentFromEntity(ent.Owner, reagentStep.ReagentId, reagentStep.Quantity);
        if (!reagent)
            return false;

        return true;
    }
}


