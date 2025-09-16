using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

public sealed partial class DiseaseSymptomSystem
{
    /// <summary>
    /// Replaces the current disease with another disease prototype, starting at a given stage.
    /// </summary>
    private void DoTransitionDisease(Entity<DiseaseCarrierComponent> ent, DiseasePrototype current, SymptomTransitionDisease trans)
    {
        if (string.IsNullOrWhiteSpace(trans.Disease) || trans.Disease == current.ID)
            return;

        if (ent.Comp.ActiveDiseases.ContainsKey(current.ID))
            ent.Comp.ActiveDiseases.Remove(current.ID);

        _disease.Infect(ent.Owner, trans.Disease, Math.Max(1, trans.StartStage));
    }
}
