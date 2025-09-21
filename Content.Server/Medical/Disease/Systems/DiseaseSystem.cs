using System;
using System.Linq;
using Robust.Shared.Collections;
using Content.Server.Body.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Medical.Disease;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Server.Popups;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Disease;

/// <summary>
/// Server system that progresses diseases, triggers symptom behaviors, and handles spread/immunity.
/// </summary>
public sealed partial class DiseaseSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DiseaseSymptomSystem _symptoms = default!;
    [Dependency] private readonly DiseaseCureSystem _cure = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly InternalsSystem _internals = default!;

    private static readonly TimeSpan TickDelay = TimeSpan.FromSeconds(1);

    /// <inheritdoc/>
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

            // Incubation: if still incubating, skip symptoms and spreading-level logic.
            if (ent.Comp.IncubatingUntil.TryGetValue(diseaseId, out var until) && until > _timing.CurTime)
                continue;

            // Progression: scale advance chance strictly according to StageSpeed and time between ticks.
            var newStage = AdvanceStage(disease, stage);

            if (newStage != stage)
            {
                ent.Comp.ActiveDiseases[diseaseId] = newStage;
                dirty = true;
            }

            // Trigger configured stage effects.
            TriggerStage(ent, disease, newStage);

            // Attempt passive cure steps for this disease.
            _cure.TriggerCureSteps(ent, disease);
        }

        foreach (var id in toRemove)
        {
            ent.Comp.ActiveDiseases.Remove(id);
            dirty = true;
        }

        if (dirty)
            Dirty(ent);
    }

    private int AdvanceStage(DiseasePrototype disease, int currentStage)
    {
        var perTickAdvance = Math.Clamp(disease.StageSpeed, 0f, 1f);
        var maxStage = Math.Max(1, disease.Stages.Count);
        if (_random.Prob(perTickAdvance))
            return Math.Min(currentStage + 1, maxStage);

        return currentStage;
    }

    private void TriggerStage(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease, int stage)
    {
        var stageCfg = disease.Stages.FirstOrDefault(s => s.Stage == stage);
        if (stageCfg == null)
            return;

        // Stage sensations: optional lightweight popups.
        if (stageCfg.Sensation.Count > 0 && _random.Prob(stageCfg.SensationProb))
        {
            var key = _random.Pick(stageCfg.Sensation);
            _popup.PopupEntity(Loc.GetString(key), ent, ent.Owner, PopupType.Small);
        }

        foreach (var symptomId in stageCfg.Symptoms)
        {
            if (!_prototypes.TryIndex<DiseaseSymptomPrototype>(symptomId, out var symptom))
                continue;

            // Skip if this symptom is currently suppressed by a symptom-level cure.
            if (ent.Comp.SuppressedSymptoms.TryGetValue(symptomId, out var value) && value > _timing.CurTime)
                continue;

            var prob = symptom.TriggerProb;
            if (prob <= 0f)
                continue;

            if (!_random.Prob(prob))
                continue;

            _symptoms.TriggerSymptom(ent, disease, symptom);
        }
    }

    /// <summary>
    /// Validates if an entity can be infected with a particular disease (alive and prototype exists).
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
    /// Infects an entity if eligible, when it has a carrier component, and sets the initial stage.
    /// </summary>
    public bool Infect(EntityUid uid, string diseaseId, int startStage = 1)
    {
        if (!_prototypes.HasIndex<DiseasePrototype>(diseaseId))
            return false;

        if (!TryComp<DiseaseCarrierComponent>(uid, out var carrier))
            return false;

        // Only initialize stage and incubation when this disease is first added to the carrier.
        if (!carrier.ActiveDiseases.ContainsKey(diseaseId))
        {
            // Set initial stage.
            carrier.ActiveDiseases[diseaseId] = startStage;

            // Schedule incubation window if configured; during incubation symptoms/spread are suppressed.
            var proto = _prototypes.Index<DiseasePrototype>(diseaseId);
            if (proto.IncubationSeconds > 0)
                carrier.IncubatingUntil[diseaseId] = _timing.CurTime + TimeSpan.FromSeconds(proto.IncubationSeconds);
        }

        carrier.NextTick = _timing.CurTime + TickDelay;
        Dirty(uid, carrier);
        return true;
    }

    /// <summary>
    /// Adjusts airborne infection chance for PPE/internals on the target.
    /// </summary>
    public float AdjustAirborneChanceForProtection(EntityUid target, float baseChance, DiseasePrototype disease)
    {
        var chance = baseChance;

        if (!disease.IgnoreMaskPPE)
        {
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

    /// <summary>
    /// Adjusts contact infection chance for PPE on the target.
    /// </summary>
    public float AdjustContactChanceForProtection(EntityUid target, float baseChance)
    {
        var chance = baseChance;
        foreach (var (slot, mult) in DiseaseEffectiveness.ContactSlots)
        {
            if (_inventory.TryGetSlotEntity(target, slot, out _))
                chance *= mult;
        }

        return MathF.Max(0f, MathF.Min(1f, chance));
    }
}
