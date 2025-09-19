using Content.Server.Body.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Disease;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
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

public sealed partial class DiseaseCureSystem
{
    /// <summary>
    /// Cures the disease if the bloodstream chemical solution contains enough of the reagent.
    /// Does not consume the reagent.
    /// </summary>
    private bool DoCureReagent(Entity<DiseaseCarrierComponent> ent, CureReagent cure, DiseasePrototype disease)
    {
        if (!TryComp<BloodstreamComponent>(ent.Owner, out var bloodstream))
            return false;

        // Resolve the chemicals solution reliably (cache may be null).
        if (!_solutions.ResolveSolution(ent.Owner, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution, out var chemSolution))
            return false;

        var quant = chemSolution.GetTotalPrototypeQuantity(cure.ReagentId);
        return quant >= cure.Quantity;
    }
}

public sealed partial class CureReagent
{
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
