namespace Content.Shared.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomTemperature : SymptomBehavior
{
    /// <summary>
    /// Target body temperature (K) to move towards.
    /// </summary>
    [DataField]
    public float TargetTemperature { get; private set; } = 310.15f;

    /// <summary>
    /// Maximum delta (K) applied per trigger.
    /// </summary>
    [DataField]
    public float StepTemperature { get; private set; } = 0.5f;
}
