namespace Content.Shared.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomNarcolepsy : SymptomBehavior
{
    /// <summary>
    /// Chance (0-1) to fall asleep per trigger.
    /// </summary>
    [DataField]
    public float SleepChance { get; private set; } = 0.6f;

    /// <summary>
    /// Forced sleep duration in seconds.
    /// </summary>
    [DataField("sleepDuration")]
    public float SleepDurationSeconds { get; private set; } = 6.0f;
}
