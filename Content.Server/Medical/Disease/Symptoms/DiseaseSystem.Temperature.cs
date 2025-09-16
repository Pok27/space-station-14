using System;
using Content.Server.Temperature.Components;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

public sealed partial class DiseaseSymptomSystem
{
    /// <summary>
    /// Adjusts the carrier's body temperature towards a target in small heat steps.
    /// </summary>
    private void DoTemperature(Entity<DiseaseCarrierComponent> ent, SymptomTemperature temp)
    {
        if (!TryComp<TemperatureComponent>(ent.Owner, out var temperature))
            return;

        var target = temp.TargetTemperature;
        var current = temperature.CurrentTemperature;
        if (Math.Abs(current - target) < 0.01f)
            return;

        var degrees = Math.Sign(target - current) * Math.Min(Math.Abs(target - current), temp.StepTemperature);
        var heatCap = _temperature.GetHeatCapacity(ent.Owner);
        var heat = degrees * heatCap;
        _temperature.ChangeHeat(ent.Owner, heat, ignoreHeatResistance: true, temperature);
    }
}
