using System;
using Content.Server.Body.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Medical.Disease;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Disease;

/// <summary>
/// Handles airborne disease spread in a periodic.
/// Also exposes a helper for symptom-driven airborne bursts.
/// </summary>
public sealed class AirborneDiseaseSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly DiseaseSystem _disease = default!;
    [Dependency] private readonly InternalsSystem _internals = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DiseaseCarrierComponent>();
        while (query.MoveNext(out var uid, out var carrier))
        {
            if (carrier.ActiveDiseases.Count == 0)
                continue;

            // Piggyback on the disease tick cadence to avoid extra scheduling overhead.
            if (carrier.NextTick > now)
                continue;

            foreach (var (diseaseId, _) in carrier.ActiveDiseases)
            {
                if (!_prototypes.TryIndex<DiseasePrototype>(diseaseId, out var disease))
                    continue;

                if (!disease.SpreadFlags.Contains(DiseaseSpreadFlags.Airborne))
                    continue;

                if (!_random.Prob(disease.AirborneTickChance))
                    continue;

                TryAirborneSpread(uid, disease);
            }
        }
    }

    /// <summary>
    /// Performs a one-off airborne spread attempt from a source carrier using disease parameters.
    /// </summary>
    public void TryAirborneSpread(EntityUid source, DiseasePrototype disease, float? overrideRange = null, float chanceMultiplier = 1f)
    {
        if (Deleted(source))
            return;

        var mapPos = _transformSystem.GetMapCoordinates(source);
        if (mapPos.MapId == MapId.Nullspace)
            return;

        var range = overrideRange ?? disease.AirborneRange;
        var targets = _lookup.GetEntitiesInRange(mapPos, range, LookupFlags.Dynamic | LookupFlags.Sundries);
        foreach (var other in targets)
        {
            if (other == source)
                continue;

            // Simple LOS/obstacle check, similar to atmospheric blocking behavior.
            if (!_transformSystem.InRange(source, other, range)
                || !EntityManager.TryGetComponent(source, out TransformComponent? srcXform)
                || !EntityManager.TryGetComponent(other, out TransformComponent? _))
                continue;

            // Try to avoid through-walls spread.
            if (!_interaction.InRangeUnobstructed(source, other, range))
                continue;

            var chance = Math.Clamp(disease.AirborneInfect * chanceMultiplier, 0f, 1f);
            chance = AdjustChanceForProtection(other, chance, disease);
            _disease.TryInfectWithChance(other, disease.ID, chance);
        }
    }

    /// <summary>
    /// Coarse PPE/internals-based reduction.
    /// </summary>
    private float AdjustChanceForProtection(EntityUid target, float baseChance, DiseasePrototype disease)
    {
        var chance = baseChance;

        if (!disease.IgnoreMaskPPE)
        {
            // Internals (active breathing gear).
            if (_internals.AreInternalsWorking(target))
                chance *= DiseaseEffectiveness.InternalsMultiplier;

            foreach (var (slot, mult) in DiseaseEffectiveness.AirborneSlots)
            {
                if (slot == "mask")
                {
                    if (_inventory.TryGetSlotEntity(target, slot, out var maskUid)
                        && TryComp<MaskComponent>(maskUid, out var mask) && !mask.IsToggled)
                        chance *= mult;
                    continue;
                }

                if (_inventory.TryGetSlotEntity(target, slot, out _))
                    chance *= mult;
            }
        }

        return MathF.Max(0f, MathF.Min(1f, chance));
    }
}


