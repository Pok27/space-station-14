using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

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
