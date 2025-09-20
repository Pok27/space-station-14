using System;
using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical.Disease;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Collections;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.Disease;

/// <summary>
/// Decays disease residue on tiles/items and infects entities on direct contact.
/// </summary>
public sealed class DiseaseResidueSystem : EntitySystem
{
    [Dependency] private readonly DiseaseSystem _disease = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private static readonly TimeSpan CarrierTickDelay = TimeSpan.FromSeconds(2);

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiseaseResidueComponent, ContactInteractionEvent>(OnResidueContact);
        SubscribeLocalEvent<DiseaseCarrierComponent, ContactInteractionEvent>(OnCarrierContact);

        SubscribeLocalEvent<DiseaseCarrierComponent, ComponentStartup>(OnCarrierStartup);
        SubscribeLocalEvent<DiseaseCarrierComponent, ComponentShutdown>(OnCarrierShutdown);

        SubscribeLocalEvent<DiseaseCarrierComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<DiseaseCarrierComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DiseaseResidueComponent>();
        while (query.MoveNext(out var uid, out var residue))
        {
            // Decay per-disease intensities
            var decay = residue.DecayPerSecond * (float) frameTime;
            var toRemoveAfterDecay = new ValueList<string>();
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
        }
    }

    /// <summary>
    /// Handles contact spread when a user uses an item on a diseased target (or vice versa).
    /// Moved from DiseaseSystem.
    /// </summary>
    private void OnAfterInteractUsing(Entity<DiseaseCarrierComponent> target, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.User == args.Target)
            return;

        var user = args.User;
        var other = target.Owner;

        var userHas = TryComp<DiseaseCarrierComponent>(user, out var userCarrier) && userCarrier.ActiveDiseases.Count > 0;
        var targetHas = target.Comp.ActiveDiseases.Count > 0;

        if (!userHas && !targetHas)
            return;

        if (userHas)
        {
            foreach (var (id, _) in userCarrier!.ActiveDiseases)
            {
                var proto = _prototypes.Index<DiseasePrototype>(id);
                if (proto.SpreadFlags.Contains(DiseaseSpreadFlags.Contact))
                    _disease.TryInfectWithChance(other, id, proto.ContactInfect);
            }
        }

        if (targetHas)
        {
            foreach (var (id, _) in target.Comp.ActiveDiseases)
            {
                var proto = _prototypes.Index<DiseasePrototype>(id);
                if (proto.SpreadFlags.Contains(DiseaseSpreadFlags.Contact))
                    _disease.TryInfectWithChance(user, id, proto.ContactInfect);
            }
        }
    }

    /// <summary>
    /// Schedules the next processing tick for a carrier if needed.
    /// </summary>
    private void EnsureTick(Entity<DiseaseCarrierComponent> ent)
    {
        if (ent.Comp.NextTick <= _timing.CurTime)
            ent.Comp.NextTick = _timing.CurTime + CarrierTickDelay;
    }

    private void OnCarrierStartup(Entity<DiseaseCarrierComponent> ent, ref ComponentStartup args)
    {
        EnsureTick(ent);
    }

    private void OnCarrierShutdown(Entity<DiseaseCarrierComponent> ent, ref ComponentShutdown args)
    {
        // no-op
    }

    private void OnUnpaused(Entity<DiseaseCarrierComponent> ent, ref EntityUnpausedEvent args)
    {
        EnsureTick(ent);
    }

    /// <summary>
    /// Attempts contact-based infection and reduces residue intensity per contact.
    /// </summary>
    private void OnResidueContact(EntityUid uid, DiseaseResidueComponent residue, ContactInteractionEvent args)
    {
        // Only living mobs that pass central check can be infected by contact
        if (TryComp<MobStateComponent>(args.Other, out var mobState) && mobState.CurrentState != MobState.Dead)
        {
            var toRemove = new ValueList<string>();
            foreach (var kv in residue.Diseases.ToArray())
            {
                var id = kv.Key;
                var intensity = kv.Value;
                InfectByContactChance(args.Other, id, intensity);

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

    /// <summary>
    /// Deposits per-disease residue intensity onto contacted entity/tile.
    /// </summary>
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

    /// <summary>
    /// Tries to infect a target via contact, scaling chance by residue intensity and disease ContactInfect.
    /// </summary>
    private void InfectByContactChance(EntityUid target, string diseaseId, float intensity)
    {
        if (!_prototypes.TryIndex<DiseasePrototype>(diseaseId, out var proto))
            return;

        if (!proto.SpreadFlags.Contains(DiseaseSpreadFlags.Contact))
            return;

        var chance = Math.Clamp(proto.ContactInfect * intensity, 0f, 1f);
        _disease.TryInfectWithChance(target, diseaseId, chance);
    }
}
