using System;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Medical.Disease;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.Disease;

/// <summary>
/// Handles airborne disease spread by listening to breathing events.
/// </summary>
public sealed class AirborneDiseaseSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly RespiratorSystem _respirator = default!;
    [Dependency] private readonly DiseaseSystem _disease = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RespiratorComponent, ExhaledGasEvent>(OnExhaledGas);
    }

    /// <summary>
    /// On exhale from a source, attempt airborne spread to nearby inhaling targets.
    /// </summary>
    private void OnExhaledGas(Entity<RespiratorComponent> source, ref ExhaledGasEvent args)
    {
        if (!TryComp<DiseaseCarrierComponent>(source, out var carrier) || carrier.ActiveDiseases.Count == 0)
            return;

        var mapPos = _transformSystem.GetMapCoordinates(source);
        if (mapPos.MapId == MapId.Nullspace)
            return;

        foreach (var (diseaseId, _) in carrier.ActiveDiseases)
        {
            if (!_prototypes.TryIndex<DiseasePrototype>(diseaseId, out var disease))
                continue;

            if (!disease.HasSpreadFlag(DiseaseSpreadFlags.Airborne))
                continue;

            var range = disease.AirborneRange;
            var targets = _lookup.GetEntitiesInRange(mapPos, range, LookupFlags.Dynamic | LookupFlags.Sundries);
            foreach (var other in targets)
            {
                if (other == source.Owner)
                    continue;

                if (!_interaction.InRangeUnobstructed(source.Owner, other, range))
                    continue;

                if (!TryComp<RespiratorComponent>(other, out var targetResp) || targetResp.Status != RespiratorStatus.Inhaling)
                    continue;

                // Ensure target is currently capable of breathing (not crit etc.).
                if (!_respirator.IsBreathing((other, null)))
                    continue;

                var chance = AdjustChanceForPPE(other, disease.AirborneTickChance, disease);
                _disease.TryInfectWithChance(other, diseaseId, chance);
            }
        }
    }


    /// <summary>
    /// Applies simple PPE modifiers (mask slots) to airborne infection chance.
    /// </summary>
    private float AdjustChanceForPPE(EntityUid target, float baseChance, DiseasePrototype disease)
    {
        if (!disease.IgnoreMaskPPE && _inventory.TryGetSlotEntity(target, "mask", out var maskUid) && TryComp<MaskComponent>(maskUid, out var mask))
        {
            if (!mask.IsToggled)
                baseChance *= 0.5f;
        }

        return MathF.Max(0f, MathF.Min(1f, baseChance));
    }
}


