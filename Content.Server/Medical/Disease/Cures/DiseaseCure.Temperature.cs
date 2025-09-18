using System;
using Content.Server.Temperature.Components;
using Content.Shared.Medical.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class CureTemperature : CureStep
{
    /// <summary>
    /// Minimum allowed body temperature (K).
    /// </summary>
    [DataField("min")]
    public float MinTemperature { get; private set; } = 273.15f;

    /// <summary>
    /// Maximum allowed body temperature (K).
    /// </summary>
    [DataField("max")]
    public float MaxTemperature { get; private set; } = 310.15f;

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
        if (temperature.CurrentTemperature < cure.MinTemperature || temperature.CurrentTemperature > cure.MaxTemperature)
        {
            state.Ticker = 0;
            return false;
        }

        state.Ticker++;
        return state.Ticker >= cure.RequiredSeconds;
    }
}

public sealed partial class CureTemperature
{
    public override IEnumerable<string> BuildDiagnoserLines(IPrototypeManager prototypes)
    {
        var min = MinTemperature;
        var max = MaxTemperature;

        if (float.IsFinite(min) && float.IsFinite(max))
        {
            yield return Loc.GetString("diagnoser-cure-temp", ("min", (int) MathF.Ceiling(min)), ("max", (int) MathF.Floor(max)));
            yield break;
        }

        if (float.IsFinite(min))
        {
            yield return Loc.GetString("diagnoser-cure-temp-min", ("min", (int) MathF.Ceiling(min)));
            yield break;
        }

        if (float.IsFinite(max))
        {
            yield return Loc.GetString("diagnoser-cure-temp-max", ("max", (int) MathF.Floor(max)));
            yield break;
        }
    }
}
