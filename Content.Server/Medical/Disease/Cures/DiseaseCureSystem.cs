using System;
using System.Linq;
using System.Collections.Generic;
using Content.Shared.Medical.Disease;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Server.Medical.Disease;

public sealed partial class DiseaseCureSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    /// <inheritdoc/>
    /// <summary>
    /// Dispatches a configured cure step to the corresponding handler.
    /// </summary>
    private bool ExecuteCureStep(Entity<DiseaseCarrierComponent> ent, CureStep step, DiseasePrototype disease)
    {
        switch (step)
        {
            case CureReagent reagent:
                return DoCureReagent(ent, reagent, disease);

            case CureBedrest bedrest:
                return DoCureBedrest(ent, bedrest, disease);

            case CureTemperature temp:
                return DoCureTemperature(ent, temp, disease);

            case CureWait wait:
                return DoCureWait(ent, wait, disease);

            default:
                return false;
        }
    }

    /// <summary>
    /// Attempts to apply cure steps for a disease on the provided carrier.
    /// </summary>
    public void TriggerCureSteps(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease)
    {
        if (!ent.Comp.ActiveDiseases.TryGetValue(disease.ID, out var stageNum))
            return;

        var stageCfg = disease.Stages.FirstOrDefault(s => s.Stage == stageNum);
        if (stageCfg == null)
            return;

        // Prefer stage-level cure steps when available
        var applicable = stageCfg.CureSteps.Count > 0
            ? stageCfg.CureSteps
            : disease.CureSteps;

        foreach (var step in applicable)
        {
            if (ExecuteCureStep(ent, step, disease))
                ApplyCureDisease(ent, disease, stageCfg.Symptoms);
        }

        // Also attempt symptom-level cure steps defined on the symptom prototypes for this stage.
        foreach (var symptomId in stageCfg.Symptoms)
        {
            if (!_prototypeManager.TryIndex<DiseaseSymptomPrototype>(symptomId, out var symptomProto))
                continue;

            // If symptom is currently suppressed (recently treated), skip any further treatment
            if (ent.Comp.SuppressedSymptoms.TryGetValue(symptomId, out var suppressUntil) && suppressUntil > _timing.CurTime)
                continue;

            if (symptomProto.CureSteps.Count == 0)
                continue;

            foreach (var step in symptomProto.CureSteps)
            {
                if (ExecuteCureStep(ent, step, disease))
                    ApplyCureSymptom(ent, disease, symptomId);
            }
        }
    }

    /// <summary>
    /// Removes the disease, applies post-cure immunity, and triggers symptom cleanup hooks.
    /// </summary>
    public void ApplyCureDisease(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease, IReadOnlyList<ProtoId<DiseaseSymptomPrototype>> stageSymptoms)
    {
        if (!ent.Comp.ActiveDiseases.ContainsKey(disease.ID))
            return;

        ent.Comp.ActiveDiseases.Remove(disease.ID);
        ApplyPostCureImmunity(ent.Comp, disease);

        NotifyDiseaseCured(ent, disease, stageSymptoms);
    }

    /// <summary>
    /// Suppresses the given symptom for its configured duration and notifies hooks.
    /// </summary>
    public void ApplyCureSymptom(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease, string symptomId)
    {
        if (!_prototypeManager.TryIndex<DiseaseSymptomPrototype>(symptomId, out var symptomProto))
            return;

        var duration = symptomProto.CureDuration;
        if (duration <= 0f)
            return;

        ent.Comp.SuppressedSymptoms[symptomId] = _timing.CurTime + TimeSpan.FromSeconds(duration);

        NotifySymptomCured(ent, disease, symptomId);
    }

    /// <summary>
    /// Writes or raises the immunity strength for the cured disease on the carrier.
    /// </summary>
    private void ApplyPostCureImmunity(DiseaseCarrierComponent comp, DiseasePrototype disease)
    {
        var strength = disease.PostCureImmunity;

        if (comp.Immunity.TryGetValue(disease.ID, out var existing))
            comp.Immunity[disease.ID] = MathF.Max(existing, strength);
        else
            comp.Immunity[disease.ID] = strength;
    }

    /// <summary>
    /// Invokes <see cref="SymptomBehavior.OnDiseaseCured"/> on behaviors for the symptoms present on the cured stage only.
    /// </summary>
    private void NotifyDiseaseCured(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease, IReadOnlyList<ProtoId<DiseaseSymptomPrototype>> stageSymptoms)
    {
        foreach (var symptomId in stageSymptoms)
        {
            if (!_prototypeManager.TryIndex<DiseaseSymptomPrototype>(symptomId, out var symptomProto))
                continue;

            foreach (var behavior in symptomProto.Behaviors)
                behavior.OnDiseaseCured(ent.Owner, disease);
        }
    }

    /// <summary>
    /// Invokes <see cref="SymptomBehavior.OnSymptomCured"/> for the behaviors of the cured symptom.
    /// </summary>
    private void NotifySymptomCured(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease, string symptomId)
    {
        if (!_prototypeManager.TryIndex<DiseaseSymptomPrototype>(symptomId, out var symptomProto))
            return;

        foreach (var behavior in symptomProto.Behaviors)
            behavior.OnSymptomCured(ent.Owner, disease, symptomId);
    }

    /// <summary>
    /// Runtime per-step state stored in the system.
    /// </summary>
    private sealed class CureState
    {
        public float Ticker;
    }

    private readonly Dictionary<(EntityUid, string, CureStep), CureState> _cureStates = new();

    /// <summary>
    /// Retrieves the runtime state for the given (entity, disease, step), creating it if missing.
    /// </summary>
    private CureState GetState(EntityUid uid, string diseaseId, CureStep step)
    {
        var key = (uid, diseaseId, step);
        if (!_cureStates.TryGetValue(key, out var state))
        {
            state = new CureState();
            _cureStates[key] = state;
        }
        return state;
    }
}
