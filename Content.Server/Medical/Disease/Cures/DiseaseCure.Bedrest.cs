using System;
using Content.Shared.Bed.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Medical.Disease;
using Robust.Shared.Prototypes;

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

public sealed partial class CureBedrest
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly DiseaseCureSystem _cureSystem = default!;

    /// <summary>
    /// Accumulates time while the entity is buckled to a bed.
    /// Sleeping multiplies accumulation. While not on bed, progress decays.
    /// </summary>
    public override bool OnCure(EntityUid uid, DiseasePrototype disease)
    {
        if (RequiredSeconds <= 0f)
            return false;

        var onBed = false;
        if (_entityManager.TryGetComponent(uid, out BuckleComponent? buckle) && buckle.BuckledTo is { } strappedTo)
            onBed = _entityManager.HasComponent<HealOnBuckleComponent>(strappedTo);

        var state = _cureSystem.GetState(uid, disease.ID, this);

        if (onBed)
        {
            var sleeping = _entityManager.HasComponent<SleepingComponent>(uid);
            var mult = sleeping ? MathF.Max(1f, SleepMultiplier) : 1f;
            state.Ticker += mult;
        }
        else if (AwakeDecayPerSecond > 0f)
        {
            state.Ticker = MathF.Max(0f, state.Ticker - AwakeDecayPerSecond);
        }

        if (state.Ticker < RequiredSeconds)
            return false;

        state.Ticker = 0;
        return true;
    }
    public override IEnumerable<string> BuildDiagnoserLines(IPrototypeManager prototypes)
    {
        // Round seconds to int for display
        var time = (int) MathF.Ceiling(RequiredSeconds);
        var sleepMult = SleepMultiplier;

        // Calculate equivalent sleeping time if multiplier > 1
        var sleepSeconds = (int) MathF.Ceiling(RequiredSeconds / MathF.Max(1f, sleepMult));
        yield return Loc.GetString("diagnoser-cure-bedrest", ("time", time), ("sleep", sleepSeconds));
    }
}


