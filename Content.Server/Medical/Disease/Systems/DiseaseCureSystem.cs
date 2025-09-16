using System;
using System.Linq;
using Content.Shared.Medical.Disease;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.Server.Medical.Disease;

public sealed partial class DiseaseCureSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DiseaseSymptomSystem _symptoms = default!;

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

            case CureSleep sleep:
                return DoCureSleep(ent, sleep, disease);

            case CureTemperature temp:
                return DoCureTemperature(ent, temp, disease);

            case CureTime time:
                return DoCureTime(ent, time, disease);

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
        var applicable = (stageCfg.CureSteps != null && stageCfg.CureSteps.Count > 0)
            ? stageCfg.CureSteps
            : disease.CureSteps;

        foreach (var step in applicable)
        {
            if (ExecuteCureStep(ent, step, disease))
                ApplyCureDisease(ent, disease);
        }

        // Also attempt symptom-level cure steps defined on the symptom prototypes for this stage.
        foreach (var symptomId in stageCfg.Symptoms)
        {
            if (!_prototypeManager.TryIndex<DiseaseSymptomPrototype>(symptomId, out var symptomProto))
                continue;

            // If symptom is currently suppressed (recently treated), skip any further treatment
            if (ent.Comp.SuppressedSymptoms.TryGetValue(symptomId, out var suppressUntil) && suppressUntil > _timing.CurTime)
                continue;

            if (symptomProto.CureSteps == null || symptomProto.CureSteps.Count == 0)
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
    private void ApplyCureDisease(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease)
    {
        if (!ent.Comp.ActiveDiseases.ContainsKey(disease.ID))
            return;

        ent.Comp.ActiveDiseases.Remove(disease.ID);
        ApplyPostCureImmunity(ent.Comp, disease);

        _symptoms.OnDiseaseCured(ent, disease);
    }

    /// <summary>
    /// Suppresses the given symptom for its configured duration and notifies hooks.
    /// </summary>
    private void ApplyCureSymptom(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease, string symptomId)
    {
        if (!_prototypeManager.TryIndex<DiseaseSymptomPrototype>(symptomId, out var symptomProto))
            return;

        var duration = symptomProto.CureDuration;
        if (duration <= 0f)
            return;

        ent.Comp.SuppressedSymptoms[symptomId] = _timing.CurTime + TimeSpan.FromSeconds(duration);

        _symptoms.OnSymptomCured(ent, disease, symptomId);
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
}
