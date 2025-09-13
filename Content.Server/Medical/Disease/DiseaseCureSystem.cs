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
using Robust.Shared.Localization;

namespace Content.Server.Medical.Disease;

public sealed class DiseaseCureSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    /// <summary>
    /// Attempts to apply cure steps for a disease on the provided carrier.
    /// </summary>
    public void TriggerCureSteps(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease)
    {
        if (ent == null)
            return;

        if (!ent.Comp.ActiveDiseases.TryGetValue(disease.ID, out var stageNum))
            return;

        // Prefer stage-level cure steps when available
        var stageCfg = disease.Stages.FirstOrDefault(s => s.Stage == stageNum);
        List<CureStep> applicable = new();

        if (stageCfg != null && stageCfg.CureSteps != null && stageCfg.CureSteps.Count > 0)
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
                ApplyPostCureImmunity(ent.Comp, disease);
        }
    }

    /// <summary>
    /// Applies a reagent cure step to the carrier for the given disease.
    /// </summary>
    private bool DoCureReagent(Entity<DiseaseCarrierComponent> ent, CureReagent reagentStep, DiseasePrototype disease)
    {
        if (!TryConsumeReagentFromEntity(ent.Owner, reagentStep.ReagentId, reagentStep.Quantity))
            return false;

        ent.Comp.ActiveDiseases.Remove(disease.ID);
        _popup.PopupEntity(Loc.GetString("disease-cured", ("disease", disease.Name)), ent.Owner, PopupType.Medium);
        return true;
    }

    private void ApplyPostCureImmunity(DiseaseCarrierComponent comp, DiseasePrototype disease)
    {
        var strength = disease.PostCureImmunityStrength;

        if (comp.Immunity.TryGetValue(disease.ID, out var existing))
            comp.Immunity[disease.ID] = MathF.Max(existing, strength);
        else
            comp.Immunity[disease.ID] = strength;
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
