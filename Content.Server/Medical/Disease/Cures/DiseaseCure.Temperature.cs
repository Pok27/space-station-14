using System;
using Content.Server.Temperature.Components;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class CureTemperature : CureStep
{
    /// <summary>
    /// Minimum allowed body temperature (K).
    /// </summary>
    [DataField]
    public float Min { get; private set; } = 273.15f;

    /// <summary>
    /// Maximum allowed body temperature (K).
    /// </summary>
    [DataField]
    public float Max { get; private set; } = 310.15f;

    /// <summary>
    /// Consecutive seconds required in range.
    /// </summary>
    [DataField]
    public int RequiredSeconds { get; private set; } = 15;
}

public sealed partial class DiseaseCureSystem
{
    /// <summary>
    /// Cures the disease after spending consecutive time within a temperature range.
    /// </summary>
    private bool DoCureTemperature(Entity<DiseaseCarrierComponent> ent, CureTemperature cure, DiseasePrototype disease)
    {
        if (cure.RequiredSeconds <= 0f)
            return false;

        if (!TryComp<TemperatureComponent>(ent.Owner, out var temperature))
            return false;

        var state = GetState(ent.Owner, disease.ID, cure);
        if (temperature.CurrentTemperature < cure.Min || temperature.CurrentTemperature > cure.Max)
        {
            state.Ticker = 0;
            return false;
        }

        state.Ticker++;
        return state.Ticker >= cure.RequiredSeconds;
    }
}
