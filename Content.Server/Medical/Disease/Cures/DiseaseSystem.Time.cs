using System;
using Robust.Shared.Random;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

public sealed partial class DiseaseCureSystem
{
    /// <summary>
    /// Cures the disease after the infection has lasted a configured duration.
    /// </summary>
    private bool DoCureTime(Entity<DiseaseCarrierComponent> ent, CureTime timeStep, DiseasePrototype disease)
    {
        if (timeStep.RequiredSeconds <= 0f)
            return false;

        if (!ent.Comp.InfectionStart.TryGetValue(disease.ID, out var start))
            return false;

        var now = _timing.CurTime;
        if ((now - start).TotalSeconds < timeStep.RequiredSeconds)
            return false;

        if (_random.Prob(timeStep.CureChance))
            return true;

        ent.Comp.InfectionStart[disease.ID] = now;
        return false;
    }
}
