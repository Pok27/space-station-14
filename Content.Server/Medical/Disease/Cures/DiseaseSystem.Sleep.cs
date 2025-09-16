using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

public sealed partial class DiseaseCureSystem
{
    /// <summary>
    /// Cures the disease if the carrier has accumulated enough sleep time.
    /// </summary>
    private bool DoCureSleep(Entity<DiseaseCarrierComponent> ent, CureSleep sleepStep, DiseasePrototype disease)
    {
        if (sleepStep.RequiredSleepSeconds <= 0f)
            return false;

        var accumulated = ent.Comp.SleepAccumulation.TryGetValue(disease.ID, out var acc) ? acc : 0f;
        if (accumulated < sleepStep.RequiredSleepSeconds)
            return false;

        ent.Comp.SleepAccumulation[disease.ID] = 0f;
        return true;
    }
}
