using System;
using System.Linq;
using Robust.Shared.Collections;
using Content.Server.Medical;
using Content.Shared.Medical.Disease;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Server.Popups;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Content.Shared.Inventory;
using Content.Shared.Clothing.Components;
using Content.Server.Body.Systems;

namespace Content.Server.Medical.Disease;

/// <summary>
/// Server system that progresses diseases, triggers symptom behaviors, and handles spread/immunity.
/// </summary>
public sealed class DiseaseSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DiseaseSymptomSystem _symptoms = default!;
    [Dependency] private readonly DiseaseCureSystem _cure = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly RespiratorSystem _respirator = default!;
    private static readonly TimeSpan TickDelay = TimeSpan.FromSeconds(2);

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DiseaseCarrierComponent, ComponentStartup>(OnCarrierStartup);
        SubscribeLocalEvent<DiseaseCarrierComponent, ComponentShutdown>(OnCarrierShutdown);
        SubscribeLocalEvent<DiseaseCarrierComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<DiseaseCarrierComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
    }

    /// <summary>
    /// Handles contact spread when a user uses an item on a diseased target (or vice versa).
    /// </summary>
    private void OnAfterInteractUsing(Entity<DiseaseCarrierComponent> target, ref AfterInteractUsingEvent args)
    {
        // Contact spread: user uses some item on a diseased target or vice versa
        if (!args.CanReach || args.User == args.Target)
            return;

        // If either side is infected and the other is not immune, try infect.
        var user = args.User;
        var other = target.Owner;

        var userHas = TryComp<DiseaseCarrierComponent>(user, out var userCarrier) && userCarrier.ActiveDiseases.Count > 0;
        var targetHas = target.Comp.ActiveDiseases.Count > 0;

        if (!userHas && !targetHas)
            return;

        // Prefer spreading from source with diseases to the other.
        if (userHas)
        {
            foreach (var (id, _) in userCarrier!.ActiveDiseases)
            {
                // small chance on contact, with central eligibility
                var proto = _prototypes.Index<DiseasePrototype>(id);
                if (HasSpreadFlag(proto, DiseaseSpreadFlags.Contact))
                    TryInfectWithChance(other, id, proto.ContactInfect);
            }
        }

        if (targetHas)
        {
            foreach (var (id, _) in target.Comp.ActiveDiseases)
            {
                var proto = _prototypes.Index<DiseasePrototype>(id);
                if (HasSpreadFlag(proto, DiseaseSpreadFlags.Contact))
                    TryInfectWithChance(user, id, proto.ContactInfect);
            }
        }
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
    /// Schedules the next processing tick for a carrier if needed.
    /// </summary>
    private void EnsureTick(Entity<DiseaseCarrierComponent> ent)
    {
        if (ent.Comp.NextTick <= _timing.CurTime)
            ent.Comp.NextTick = _timing.CurTime + TickDelay;
    }

    /// <summary>
    /// Processes carriers on their scheduled ticks.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DiseaseCarrierComponent>();
        var now = _timing.CurTime;
        var carriersToProcess = new List<(EntityUid uid, DiseaseCarrierComponent carrier)>();

        while (query.MoveNext(out var uid, out var carrier))
        {
            if (carrier.NextTick > now)
                continue;

            carrier.NextTick = now + TickDelay;
            carriersToProcess.Add((uid, carrier));
        }

        foreach (var (uid, carrier) in carriersToProcess)
        {
            ProcessCarrier((uid, carrier));
        }
    }

    /// <summary>
    /// Advances disease stages and triggers symptom behaviors when eligible.
    /// Removes invalid diseases.
    /// </summary>
    private void ProcessCarrier(Entity<DiseaseCarrierComponent> ent)
    {
        if (ent.Comp.ActiveDiseases.Count == 0)
            return;

        var toRemove = new ValueList<string>();
        var dirty = false;

        foreach (var (diseaseId, stage) in ent.Comp.ActiveDiseases.ToArray())
        {
            if (!_prototypes.TryIndex<DiseasePrototype>(diseaseId, out var disease))
            {
                toRemove.Add(diseaseId);
                continue;
            }

            // Progression: scale advance chance strictly according to StageSpeed and time between ticks.
            // Cap by number of defined stages (or at least 1 if not configured).
            var newStage = stage;
            var perTickAdvance = Math.Clamp(disease.StageSpeed * 0.01f, 0f, 1f);
            var maxStage = Math.Max(1, disease.Stages.Count);
            if (_random.Prob(perTickAdvance))
                newStage = Math.Min(stage + 1, maxStage);

            if (newStage != stage)
            {
                ent.Comp.ActiveDiseases[diseaseId] = newStage;
                dirty = true;
            }

            // Trigger symptoms configured for this stage
            if (disease.Stages.Count > 0)
            {
                var stageCfg = disease.Stages.FirstOrDefault(s => s.Stage == newStage);
                if (stageCfg != null)
                {
                    // Stage sensations: optional lightweight popups
                    if (stageCfg.Sensation.Count > 0 && _random.Prob(stageCfg.SensationProbability))
                    {
                        var key = _random.Pick(stageCfg.Sensation);
                        _popup.PopupEntity(Loc.GetString(key), ent, ent.Owner, PopupType.Small);
                    }

                    foreach (var symptomId in stageCfg.Symptoms)
                    {
                        if (!_prototypes.TryIndex<DiseaseSymptomPrototype>(symptomId, out var symptom))
                            continue;

                        var prob = symptom.TriggerProbability;
                        if (prob <= 0f)
                            continue;

                        if (!_random.Prob(prob))
                            continue;

                        _symptoms.TriggerSymptom(ent, disease, symptom);
                    }
                }
            }

            // Attempt passive cure steps for this disease.
            _cure.TriggerCureSteps(ent, disease);

            // Attempt airborne spread once per tick per disease if applicable.
            if (HasSpreadFlag(disease, DiseaseSpreadFlags.Airborne) && _random.Prob(disease.AirborneTickChance))
                SpreadAirborne(ent, disease, disease.AirborneRange, disease.AirborneInfect);
        }

        foreach (var id in toRemove)
        {
            ent.Comp.ActiveDiseases.Remove(id);
            dirty = true;
        }

        if (dirty)
            Dirty(ent);
    }

    /// <summary>
    /// Tries to infect nearby entities through air, honoring obstructions and PPE.
    /// </summary>
    private void SpreadAirborne(Entity<DiseaseCarrierComponent> source, DiseasePrototype disease, float range, float baseChance)
    {

        var mapPos = _transformSystem.GetMapCoordinates(source);
        if (mapPos.MapId == MapId.Nullspace)
            return;

        var ents = _lookup.GetEntitiesInRange(mapPos, range, LookupFlags.Dynamic | LookupFlags.Sundries);
        foreach (var other in ents)
        {
            if (other == source.Owner)
                continue;

            if (!TryComp<MobStateComponent>(other, out var mobState) || mobState.CurrentState == MobState.Dead)
                continue;

            if (!_interaction.InRangeUnobstructed(source.Owner, other, range))
                continue;

            // Only infect if the target is actually breathing.
            if (!_respirator.IsBreathing((other, null)))
                continue;

            var chance = AdjustChanceForPPE(other, baseChance, disease);
            TryInfectWithChance(other, disease.ID, chance);
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

    /// <summary>
    /// Validates if an entity can be infected with a particular disease (alive, prototype exists, not immune).
    /// </summary>
    public bool CanBeInfected(EntityUid uid, string diseaseId)
    {
        if (!_prototypes.HasIndex<DiseasePrototype>(diseaseId))
            return false;

        if (!TryComp<MobStateComponent>(uid, out var mobState) || mobState.CurrentState == MobState.Dead)
            return false;

        return true;
    }

    /// <summary>
    /// Convenience: rolls probability, validates eligibility, then infects.
    /// </summary>
    public bool TryInfectWithChance(EntityUid uid, string diseaseId, float probability, int startStage = 1)
    {
        if (!CanBeInfected(uid, diseaseId))
            return false;

        if (!_random.Prob(probability))
            return false;

        if (TryComp<DiseaseCarrierComponent>(uid, out var carrier) && carrier.Immunity.TryGetValue(diseaseId, out var immunityStrength))
        {
            // roll against immunity strength: immunityStrength of 1.0 blocks infection always, 0.0 never.
            if (_random.Prob(immunityStrength))
                return false;
        }

        return Infect(uid, diseaseId, startStage);
    }

    /// <summary>
    /// Infects an entity if eligible, ensuring a carrier component and initial stage.
    /// </summary>
    public bool Infect(EntityUid uid, string diseaseId, int startStage = 1)
    {
        if (!_prototypes.HasIndex<DiseasePrototype>(diseaseId))
            return false;

        if (!TryComp<DiseaseCarrierComponent>(uid, out var carrier))
            return false;

        if (!carrier.ActiveDiseases.ContainsKey(diseaseId))
            carrier.ActiveDiseases[diseaseId] = startStage;

        // Initialize server-side cure state for this disease.
        carrier.InfectionStart[diseaseId] = _timing.CurTime;
        carrier.CureTimers.Remove(diseaseId);
        carrier.SleepAccumulation[diseaseId] = 0f;

        carrier.NextTick = _timing.CurTime + TickDelay;
        Dirty(uid, carrier);
        return true;
    }

    /// <summary>
    /// Helper to check whether a disease prototype defines a given spread vector.
    /// Centralizes access for the new list-based `SpreadFlags` on prototypes.
    /// </summary>
    public bool HasSpreadFlag(DiseasePrototype proto, DiseaseSpreadFlags flag)
    {
        if (proto == null)
            return false;

        // If prototype uses the new list form
        if (proto.SpreadFlags != null && proto.SpreadFlags.Count > 0)
            return proto.SpreadFlags.Contains(flag);

        return false;
    }
}
