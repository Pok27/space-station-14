namespace Content.Shared.Medical.Disease;

[DataDefinition]
public sealed partial class CureTemperature : CureStep
{
    /// <summary>
    /// Minimum allowed body temperature (K).
    /// </summary>
    [DataField]
    public float MinTemperature { get; private set; } = 273.15f;

    /// <summary>
    /// Maximum allowed body temperature (K).
    /// </summary>
    [DataField]
    public float MaxTemperature { get; private set; } = 310.15f;

    /// <summary>
    /// Consecutive seconds required in range.
    /// </summary>
    [DataField]
    public float RequiredSeconds { get; private set; } = 15f;

    /// <summary>
    /// Chance to cure when the window elapses (0-1).
    /// </summary>
    [DataField]
    public float CureChance { get; private set; } = 1.0f;
}
