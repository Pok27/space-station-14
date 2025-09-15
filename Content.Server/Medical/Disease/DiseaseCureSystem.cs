using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Medical.Disease;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Server.Popups;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Localization;
using Robust.Shared.Random;
using Content.Server.Temperature.Components;

namespace Content.Server.Medical.Disease;

public sealed class DiseaseCureSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

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
                ApplyCureDisease(ent.Comp, disease, ent.Owner);
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
                    ApplyCureSymptom(ent.Comp, disease, ent.Owner, symptomId);
            }
        }
    }

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

    private void ApplyCureDisease(DiseaseCarrierComponent comp, DiseasePrototype disease, EntityUid owner)
    {
        if (!comp.ActiveDiseases.ContainsKey(disease.ID))
            return;

        comp.ActiveDiseases.Remove(disease.ID);
        ApplyPostCureImmunity(comp, disease);
    }

    private void ApplyCureSymptom(DiseaseCarrierComponent comp, DiseasePrototype disease, EntityUid owner, string symptomId)
    {
        if (!_prototypeManager.TryIndex<DiseaseSymptomPrototype>(symptomId, out var symptomProto))
            return;

        var duration = symptomProto.CureDuration;
        if (duration <= 0f)
            return;

        comp.SuppressedSymptoms[symptomId] = _timing.CurTime + TimeSpan.FromSeconds(duration);
    }

    private void ApplyPostCureImmunity(DiseaseCarrierComponent comp, DiseasePrototype disease)
    {
        var strength = disease.PostCureImmunityStrength;

        if (comp.Immunity.TryGetValue(disease.ID, out var existing))
            comp.Immunity[disease.ID] = MathF.Max(existing, strength);
        else
            comp.Immunity[disease.ID] = strength;
    }

    /// <summary>
    /// Applies a reagent cure step to the carrier for the given disease.
    /// </summary>
    private bool DoCureReagent(Entity<DiseaseCarrierComponent> ent, CureReagent reagentStep, DiseasePrototype disease)
    {
        var reagent = _solutionSystem.TryRemoveReagentFromEntity(ent.Owner, reagentStep.ReagentId, reagentStep.Quantity);
        if (!reagent)
            return false;

        return true;
    }

    private bool DoCureSleep(Entity<DiseaseCarrierComponent> ent, CureSleep sleepStep, DiseasePrototype disease)
    {
        if (sleepStep.RequiredSleepSeconds <= 0f)
            return false;

        var accumulated = ent.Comp.SleepAccumulation.TryGetValue(disease.ID, out var acc) ? acc : 0f;
        if (accumulated < sleepStep.RequiredSleepSeconds)
            return false;

        ent.Comp.SleepAccumulation[disease.ID] = 0f;
        return true;
    }

    private bool DoCureTemperature(Entity<DiseaseCarrierComponent> ent, CureTemperature tempStep, DiseasePrototype disease)
    {
        if (tempStep.RequiredSeconds <= 0f)
            return false;

        // We need entity temperature component to track consecutive time in range.
        if (!TryComp<TemperatureComponent>(ent.Owner, out var temperature))
            return false;

        var now = _timing.CurTime;
        if (temperature.CurrentTemperature < tempStep.MinTemperature || temperature.CurrentTemperature > tempStep.MaxTemperature)
        {
            ent.Comp.CureTimers.Remove(disease.ID);
            return false;
        }

        var timers = ent.Comp.CureTimers;
        if (!timers.TryGetValue(disease.ID, out var end))
        {
            timers[disease.ID] = now + TimeSpan.FromSeconds(tempStep.RequiredSeconds);
            return false;
        }

        if (end > now)
            return false;

        if (_random.Prob(tempStep.CureChance))
        {
            timers.Remove(disease.ID);
            return true;
        }

        timers[disease.ID] = now + TimeSpan.FromSeconds(tempStep.RequiredSeconds);
        return false;
    }

    private bool DoCureTime(Entity<DiseaseCarrierComponent> ent, CureTime timeStep, DiseasePrototype disease)
    {
        if (timeStep.RequiredSeconds <= 0f)
            return false;

        if (!ent.Comp.InfectionStart.TryGetValue(disease.ID, out var start))
            return false;

        var now = _timing.CurTime;
        if ((now - start).TotalSeconds < timeStep.RequiredSeconds)
            return false;

        // Roll chance to cure. If fails, restart timer from now.
        if (_random.Prob(timeStep.CureChance))
            return true;

        ent.Comp.InfectionStart[disease.ID] = now;
        return false;
    }
}
