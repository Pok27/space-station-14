using Content.Shared.FixedPoint;

namespace Content.Shared.Medical.Disease;

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
