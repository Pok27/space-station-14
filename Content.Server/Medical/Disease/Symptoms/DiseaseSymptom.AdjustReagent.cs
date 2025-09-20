using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomAdjustReagent : SymptomBehavior
{
    /// <summary>
    /// Reagent prototype ID to add (positive) or remove (negative) from the carrier's chemical solution.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent { get; private set; } = default!;

    /// <summary>
    /// Amount to change. Positive adds, negative removes.
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 Amount { get; private set; } = FixedPoint2.Zero;
}

public sealed partial class SymptomAdjustReagent
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    /// <summary>
    /// Adjust the carrier's internal chemical solution by the configured amount for the configured reagent.
    /// </summary>
    public override void OnSymptom(EntityUid uid, DiseasePrototype disease)
    {
        if (Amount == FixedPoint2.Zero)
            return;

        if (!_entMan.TryGetComponent(uid, out BloodstreamComponent? bloodstream))
            return;

        if (!_solutions.ResolveSolution(uid, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution, out _))
            return;

        if (Amount > FixedPoint2.Zero)
            _solutions.TryAddReagent(bloodstream.ChemicalSolution!.Value, Reagent, Amount, out _);
        else
            _solutions.RemoveReagent(bloodstream.ChemicalSolution!.Value, Reagent, -Amount);
    }
}


