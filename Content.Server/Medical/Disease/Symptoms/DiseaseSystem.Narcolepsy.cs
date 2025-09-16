using System;
using Content.Shared.Bed.Sleep;
using Content.Shared.Medical.Disease;
using Robust.Shared.Random;

namespace Content.Server.Medical.Disease;

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
