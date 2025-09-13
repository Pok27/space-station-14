using Content.Shared.FixedPoint;
namespace Content.Shared.Medical.Disease;

/// <summary>
/// Base class for cure step variants. Mirrors symptom behavior style so prototypes can
/// list cure steps inline as typed entries.
/// </summary>
[DataDefinition]
public abstract partial class CureStep
{
}

[DataDefinition]
public sealed partial class CureReagent : CureStep
{
    /// <summary>
    /// Reagent prototype id required to apply this cure step.
    /// </summary>
    [DataField(required: true)]
    public string ReagentId { get; private set; } = string.Empty;

    /// <summary>
    /// Amount of reagent units required to cure.
    /// </summary>
    [DataField]
    public FixedPoint2 Quantity { get; private set; } = FixedPoint2.New(1);
}
