using System;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical.Disease;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Disease;

/// <summary>
/// Decays disease residue on tiles/items and infects entities in proximity on tick.
/// </summary>
public sealed class DiseaseResidueSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly DiseaseSystem _disease = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseResidueComponent, ContactInteractionEvent>(OnResidueContact);
        SubscribeLocalEvent<DiseaseCarrierComponent, ContactInteractionEvent>(OnCarrierContact);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DiseaseResidueComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var residue, out var xform))
        {
            // Decay
            residue.Intensity -= residue.DecayPerSecond * (float) frameTime;
            if (residue.Intensity <= 0f)
            {
                RemComp<DiseaseResidueComponent>(uid);
                continue;
            }

            // Initialize tick scheduling if needed
            if (residue.NextTick > now)
                continue;

            residue.NextTick = now + residue.TickInterval;

            var mapPos = _xform.GetMapCoordinates(uid, xform);
            if (mapPos.MapId == MapId.Nullspace)
                continue;

            var chance = Math.Clamp(residue.InfectChanceBase * residue.Intensity, 0f, 1f);
            var ents = _lookup.GetEntitiesInRange(mapPos, residue.Range, LookupFlags.Dynamic);
            foreach (var ent in ents)
            {
                foreach (var id in residue.Diseases)
                {
                    var proto = _prototypes.Index<DiseasePrototype>(id);
                    if (_disease.HasSpreadFlag(proto, DiseaseSpreadFlags.Contact))
                        _disease.TryInfectWithChance(ent, id, chance);
                }
            }
        }
    }

    private void OnResidueContact(EntityUid uid, DiseaseResidueComponent residue, ContactInteractionEvent args)
    {
        var chance = Math.Clamp(residue.InfectChanceBase * residue.Intensity, 0f, 1f);
        // Only living mobs that pass central check can be infected by contact
        if (TryComp<MobStateComponent>(args.Other, out var mobState) && mobState.CurrentState != MobState.Dead)
        {
            foreach (var id in residue.Diseases)
            {
                _disease.TryInfectWithChance(args.Other, id, chance);
            }

            residue.Intensity = MathF.Max(0f, residue.Intensity - 0.1f);
        }
    }

    private void OnCarrierContact(EntityUid uid, DiseaseCarrierComponent carrier, ContactInteractionEvent args)
    {
        if (carrier.ActiveDiseases.Count == 0)
            return;

        // Only deposit residue onto non-living is fine; but if we want to mirror SS13, leaving residue on anything is allowed.
        var residue = EnsureComp<DiseaseResidueComponent>(args.Other);
        foreach (var (id, _) in carrier.ActiveDiseases)
        {
            if (!residue.Diseases.Contains(id))
                residue.Diseases.Add(id);
        }
        residue.Intensity = MathF.Min(1f, residue.Intensity + 0.2f);
    }
}
