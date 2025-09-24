using System;
using Content.Shared.Medical.Disease;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.Disease.Cures;

[DataDefinition]
public sealed partial class CureWait : CureStep
{
    /// <summary>
    /// Seconds since infection required before curing can occur.
    /// </summary>
    [DataField]
    public int RequiredSeconds { get; private set; } = 90;

    /// <summary>
    /// Chance to cure when the required time elapses (0-1).
    /// </summary>
    [DataField]
    public float CureChance { get; private set; } = 1.0f;
}

public sealed partial class CureWait
{
    [Dependency] private readonly DiseaseCureSystem _cureSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Cures the disease after the infection has lasted a configured duration.
    /// </summary>
    public override bool OnCure(EntityUid uid, DiseasePrototype disease)
    {
        if (RequiredSeconds <= 0f)
            return false;

        var state = _cureSystem.GetState(uid, disease.ID, this);
        state.Ticker++;
        if (state.Ticker < RequiredSeconds)
            return false;

        if (_random.Prob(CureChance))
        {
            state.Ticker = 0;
            return true;
        }

        state.Ticker = 0;
        return false;
    }

    public override IEnumerable<string> BuildDiagnoserLines(IPrototypeManager prototypes)
    {
        var time = (int)MathF.Ceiling(RequiredSeconds);
        yield return Loc.GetString("diagnoser-cure-time", ("time", time));
    }
}
