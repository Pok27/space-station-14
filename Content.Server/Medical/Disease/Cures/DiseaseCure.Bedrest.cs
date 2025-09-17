using System;
using Content.Shared.Bed.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class CureBedrest : CureStep
{
    /// <summary>
    /// Total required accumulated bedrest seconds to cure.
    /// </summary>
    [DataField]
    public float RequiredSeconds { get; private set; } = 30f;

    /// <summary>
    /// Multiplier to accumulation while the carrier is sleeping.
    /// </summary>
    [DataField]
    public float SleepMultiplier { get; private set; } = 3f;

    /// <summary>
    /// Decay rate of accumulation per second while not in bed.
    /// </summary>
    [DataField]
    public float AwakeDecayPerSecond { get; private set; } = 2f;
}

public sealed partial class DiseaseCureSystem
{
    /// <summary>
    /// Accumulates time while the entity is buckled to a bed.
    /// Sleeping multiplies accumulation. While not on bed, progress decays.
    /// </summary>
    private bool DoCureBedrest(Entity<DiseaseCarrierComponent> ent, CureBedrest cure, DiseasePrototype disease)
    {
        if (cure.RequiredSeconds <= 0f)
            return false;

        var onBed = false;
        if (TryComp<BuckleComponent>(ent.Owner, out var buckle) && buckle.BuckledTo is { } strappedTo)
            onBed = HasComp<HealOnBuckleComponent>(strappedTo);

        var state = GetState(ent.Owner, disease.ID, cure);

        if (onBed)
        {
            var sleeping = HasComp<SleepingComponent>(ent.Owner);
            var mult = sleeping ? MathF.Max(1f, cure.SleepMultiplier) : 1f;
            state.Ticker += mult;
        }
        else if (cure.AwakeDecayPerSecond > 0f)
        {
            state.Ticker = MathF.Max(0f, state.Ticker - cure.AwakeDecayPerSecond);
        }

        if (state.Ticker < cure.RequiredSeconds)
            return false;

        state.Ticker = 0;
        return true;
    }
}


