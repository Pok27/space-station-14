using System;
using Content.Shared.Medical.Disease;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class CureWait : CureStep
{
    /// <summary>
    /// Seconds since infection required before curing can occur.
    /// </summary>
    [DataField]
    public float RequiredSeconds { get; private set; } = 90.0f;

    /// <summary>
    /// Chance to cure when the required time elapses (0-1).
    /// </summary>
    [DataField]
    public float CureChance { get; private set; } = 1.0f;
}

public sealed partial class DiseaseCureSystem
{
    /// <summary>
    /// Cures the disease after the infection has lasted a configured duration.
    /// </summary>
    private bool DoCureWait(Entity<DiseaseCarrierComponent> ent, CureWait cure, DiseasePrototype disease)
    {
        if (cure.RequiredSeconds <= 0f)
            return false;

        var state = GetState(ent.Owner, disease.ID, cure);
        state.Ticker++;
        if (state.Ticker < cure.RequiredSeconds)
            return false;

        if (_random.Prob(cure.CureChance))
        {
            state.Ticker = 0;
            return true;
        }

        state.Ticker = 0;
        return false;
    }
}

public sealed partial class CureWait
{
    public override IEnumerable<string> BuildDiagnoserLines(IPrototypeManager prototypes)
    {
        var time = (int) MathF.Ceiling(RequiredSeconds);
        yield return Loc.GetString("diagnoser-cure-time", ("time", time));
    }
}
