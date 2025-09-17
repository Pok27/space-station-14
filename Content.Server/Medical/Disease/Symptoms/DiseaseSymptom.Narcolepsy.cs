using System;
using Content.Shared.Bed.Sleep;
using Content.Shared.Medical.Disease;
using Robust.Shared.Random;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomNarcolepsy : SymptomBehavior
{
    /// <summary>
    /// Chance (0-1) to fall asleep per trigger.
    /// </summary>
    [DataField]
    public float SleepChance { get; private set; } = 0.6f;

    /// <summary>
    /// Forced sleep duration in seconds.
    /// </summary>
    [DataField("sleepDuration")]
    public float SleepDurationSeconds { get; private set; } = 6.0f;
}

public sealed partial class DiseaseSymptomSystem
{
    /// <summary>
    /// Randomly forces the carrier to fall asleep for a configured duration.
    /// </summary>
    private void DoNarcolepsy(Entity<DiseaseCarrierComponent> ent, SymptomNarcolepsy narco)
    {
        if (!_random.Prob(narco.SleepChance))
            return;

        var dur = TimeSpan.FromSeconds(narco.SleepDurationSeconds);
        _status.TryAddStatusEffectDuration(ent.Owner, SleepingSystem.StatusEffectForcedSleeping, dur);
    }
}
