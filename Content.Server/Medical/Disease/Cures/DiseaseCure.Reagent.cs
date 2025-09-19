using Content.Server.Body.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Disease;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class CureReagent : CureStep
{
    /// <summary>
    /// Reagent prototype ID to consume.
    /// </summary>
    [DataField(required: true)]
    public string ReagentId { get; private set; } = string.Empty;

    /// <summary>
    /// Required reagent quantity in units.
    /// </summary>
    [DataField]
    public FixedPoint2 Quantity { get; private set; } = FixedPoint2.New(1);
}

public sealed partial class CureReagent
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    /// <summary>
    /// Cures the disease if the bloodstream chemical solution contains enough of the reagent.
    /// Does not consume the reagent.
    /// </summary>
    public override bool OnCure(EntityUid uid, DiseasePrototype disease)
    {
        if (!_entityManager.TryGetComponent(uid, out BloodstreamComponent? bloodstream))
            return false;

        if (!_solutions.ResolveSolution(uid, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution, out var chemSolution))
            return false;

        var quant = chemSolution.GetTotalPrototypeQuantity(ReagentId);
        return quant >= Quantity;
    }

    public override IEnumerable<string> BuildDiagnoserLines(IPrototypeManager prototypes)
    {
        var reagentName = ReagentId;
        if (prototypes.TryIndex<ReagentPrototype>(ReagentId, out var reagentProto))
            reagentName = reagentProto.LocalizedName;

        // Use FixedPoint2.ToString for locale-safe quantity
        var unitsText = Quantity.ToString();
        yield return Loc.GetString("diagnoser-cure-reagent", ("units", unitsText), ("reagent", reagentName));
    }
}
