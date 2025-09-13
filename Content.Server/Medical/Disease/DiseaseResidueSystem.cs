using System;
using System.Linq;
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
            // Decay per-disease intensities
            var decay = residue.DecayPerSecond * (float) frameTime;
            var toRemoveAfterDecay = new List<string>();
            foreach (var kv in residue.Diseases.ToArray())
            {
                var newVal = kv.Value - decay;
                if (newVal <= 0f)
                    toRemoveAfterDecay.Add(kv.Key);
                else
                    residue.Diseases[kv.Key] = newVal;
            }

            foreach (var k in toRemoveAfterDecay)
                residue.Diseases.Remove(k);

            if (residue.Diseases.Count == 0)
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

            var ents = _lookup.GetEntitiesInRange(mapPos, residue.Range, LookupFlags.Dynamic);
            foreach (var ent in ents)
            {
                foreach (var kv in residue.Diseases.ToArray())
                {
                    var id = kv.Key;
                    var intensity = kv.Value;
                    if (!_prototypes.TryIndex<DiseasePrototype>(id, out var proto))
                        continue;

                    if (!_disease.HasSpreadFlag(proto, DiseaseSpreadFlags.Contact))
                        continue;

                    var chance = Math.Clamp(proto.ContactInfect * intensity, 0f, 1f);
                    _disease.TryInfectWithChance(ent, id, chance);
                }
            }
        }
    }

    private void OnResidueContact(EntityUid uid, DiseaseResidueComponent residue, ContactInteractionEvent args)
    {
        // Only living mobs that pass central check can be infected by contact
        if (TryComp<MobStateComponent>(args.Other, out var mobState) && mobState.CurrentState != MobState.Dead)
        {
            var toRemove = new List<string>();
            foreach (var kv in residue.Diseases.ToArray())
            {
                var id = kv.Key;
                var intensity = kv.Value;
                if (!_prototypes.TryIndex<DiseasePrototype>(id, out var proto))
                    continue;

                var chance = Math.Clamp(proto.ContactInfect * intensity, 0f, 1f);
                _disease.TryInfectWithChance(args.Other, id, chance);

                // reduce intensity after contact
                var newVal = intensity - residue.ContactReduction;
                if (newVal <= 0f)
                    toRemove.Add(id);
                else
                    residue.Diseases[id] = newVal;
            }

            foreach (var k in toRemove)
                residue.Diseases.Remove(k);
        }
    }

    private void OnCarrierContact(EntityUid uid, DiseaseCarrierComponent carrier, ContactInteractionEvent args)
    {
        if (carrier.ActiveDiseases.Count == 0)
            return;

        var residue = EnsureComp<DiseaseResidueComponent>(args.Other);
        foreach (var (id, _) in carrier.ActiveDiseases)
        {
            if (!_prototypes.TryIndex<DiseasePrototype>(id, out var proto))
                continue;

            var deposit = proto.ContactDeposit;
            if (residue.Diseases.TryGetValue(id, out var cur))
                residue.Diseases[id] = MathF.Min(1f, cur + deposit);
            else
                residue.Diseases[id] = MathF.Min(1f, deposit);
        }
    }
}
