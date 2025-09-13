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

namespace Content.Server.Medical.Disease;

public sealed class DiseaseCureSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Attempts to apply cure steps for a disease on the provided carrier.
    /// </summary>
    public void TriggerCureSteps(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease)
    {
        if (ent == null)
            return;

        if (!ent.Comp.ActiveDiseases.TryGetValue(disease.ID, out var stageNum))
            return;

        var stageCfg = disease.Stages.FirstOrDefault(s => s.Stage == stageNum);
        if (stageCfg == null)
            return;

        // Prefer stage-level cure steps when available
        List<CureStep> applicable = new();
        if (stageCfg.CureSteps != null && stageCfg.CureSteps.Count > 0)
            applicable = stageCfg.CureSteps;
        else
            applicable = disease.CureSteps;

        foreach (var step in applicable)
        {
            var succeeded = false;
            switch (step)
            {
                case CureReagent reagent:
                    succeeded = DoCureReagent(ent, reagent, disease);
                    break;

                default:
                    break;
            }

            if (succeeded)
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
                var succeeded = false;
                switch (step)
                {
                    case CureReagent reagent:
                        succeeded = DoCureReagent(ent, reagent, disease);
                        break;
                    default:
                        break;
                }

                if (succeeded)
                    ApplyCureSymptom(ent.Comp, disease, ent.Owner, symptomId);
            }
        }
    }

    private void ApplyCureDisease(DiseaseCarrierComponent comp, DiseasePrototype disease, EntityUid owner)
    {
        if (!comp.ActiveDiseases.ContainsKey(disease.ID))
            return;

        comp.ActiveDiseases.Remove(disease.ID);
        _popup.PopupEntity(Loc.GetString("disease-cured", ("disease", disease.Name)), owner, PopupType.Medium);

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
        _popup.PopupEntity(Loc.GetString("disease-symptom-treated", ("symptom", symptomProto.Name)), owner, PopupType.Medium);
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
        if (!TryConsumeReagentFromEntity(ent.Owner, reagentStep.ReagentId, reagentStep.Quantity))
            return false;

        return true;
    }

    private bool TryConsumeReagentFromEntity(EntityUid uid, string reagentId, FixedPoint2 quantity)
    {
        if (!TryComp<SolutionContainerManagerComponent>(uid, out var manager))
            return false;

        var availableTotal = _solutionSystem.GetTotalPrototypeQuantity(uid, reagentId);
        if (availableTotal < quantity)
            return false;

        var remaining = quantity;
        foreach (var (name, solEnt) in _solutionSystem.EnumerateSolutions((uid, manager), includeSelf: true))
        {
            var availableInSol = solEnt.Comp.Solution.GetTotalPrototypeQuantity(reagentId);
            var toRemove = FixedPoint2.Min(remaining, availableInSol);
            if (toRemove <= FixedPoint2.New(0))
                continue;
            var removed = _solutionSystem.RemoveReagent(solEnt, reagentId, toRemove);
            remaining -= removed;
            if (remaining <= FixedPoint2.New(0))
                break;
        }

        return remaining <= FixedPoint2.New(0);
    }
}
