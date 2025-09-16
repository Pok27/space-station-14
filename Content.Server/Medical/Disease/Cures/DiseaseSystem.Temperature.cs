using System;
using Content.Server.Temperature.Components;
using Robust.Shared.Random;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

public sealed partial class DiseaseCureSystem
{
    /// <summary>
    /// Cures the disease after spending consecutive time within a temperature range.
    /// </summary>
    private bool DoCureTemperature(Entity<DiseaseCarrierComponent> ent, CureTemperature tempStep, DiseasePrototype disease)
    {
        if (tempStep.RequiredSeconds <= 0f)
            return false;

        if (!TryComp<TemperatureComponent>(ent.Owner, out var temperature))
            return false;

        var now = _timing.CurTime;
        if (temperature.CurrentTemperature < tempStep.MinTemperature || temperature.CurrentTemperature > tempStep.MaxTemperature)
        {
            ent.Comp.CureTimers.Remove(disease.ID);
            return false;
        }

        var timers = ent.Comp.CureTimers;
        if (!timers.TryGetValue(disease.ID, out var end))
        {
            timers[disease.ID] = now + TimeSpan.FromSeconds(tempStep.RequiredSeconds);
            return false;
        }

        if (end > now)
            return false;

        if (_random.Prob(tempStep.CureChance))
        {
            timers.Remove(disease.ID);
            return true;
        }

        timers[disease.ID] = now + TimeSpan.FromSeconds(tempStep.RequiredSeconds);
        return false;
    }
}
