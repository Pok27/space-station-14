using Content.Shared.FixedPoint;
using Content.Shared.Medical.Disease;
using Content.Shared.Body.Components;

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
    private bool DoCureReagent(Entity<DiseaseCarrierComponent> ent, CureReagent сure, DiseasePrototype disease)
    {
        if (!TryComp<BloodstreamComponent>(ent.Owner, out var bloodstream))
            return false;

        var quant = FixedPoint2.Zero;
        if (bloodstream.ChemicalSolution != null)
        {
            var chem = bloodstream.ChemicalSolution.Value;
            quant = chem.Comp.Solution.GetTotalPrototypeQuantity(сure.ReagentId);
        }

        return quant >= сure.Quantity;
    }
}
