using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class CureSleep : CureStep
{
    /// <summary>
    /// Total required accumulated sleep seconds.
    /// </summary>
    [DataField]
    public float RequiredSleepSeconds { get; private set; } = 0f;

    /// <summary>
    /// Decay rate of sleep accumulation while awake (seconds per second).
    /// </summary>
    [DataField]
    public float AwakeDecayPerSecond { get; private set; } = 0f;
}

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


