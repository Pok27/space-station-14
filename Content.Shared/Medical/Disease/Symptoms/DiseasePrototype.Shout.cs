using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomShout : SymptomBehavior
{
    /// <summary>
    /// Dataset of localized lines to shout.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? Pack { get; private set; }

    /// <summary>
    /// If true, suppress chat window output (bubble only).
    /// </summary>
    [DataField]
    public bool HideChat { get; private set; } = true;
}
