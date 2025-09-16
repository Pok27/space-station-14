namespace Content.Shared.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomAddComponent : SymptomBehavior
{
    /// <summary>
    /// Component registration name to add to the carrier.
    /// </summary>
    [DataField(required: true)]
    public string Component { get; private set; } = string.Empty;
}
