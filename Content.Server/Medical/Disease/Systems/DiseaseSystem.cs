using System;
using System.Linq;
using Robust.Shared.Collections;
using Content.Server.Medical;
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
    private static readonly TimeSpan TickDelay = TimeSpan.FromSeconds(2);

    /// <inheritdoc/>
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
}
