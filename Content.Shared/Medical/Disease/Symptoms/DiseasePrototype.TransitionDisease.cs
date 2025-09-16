namespace Content.Shared.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomTransitionDisease : SymptomBehavior
{
    /// <summary>
    /// Target disease prototype ID to apply.
    /// </summary>
    [DataField(required: true)]
    public string Disease { get; private set; } = string.Empty;

    /// <summary>
    /// Starting stage for the new disease.
    /// </summary>
    [DataField]
    public int StartStage { get; private set; } = 1;
}
