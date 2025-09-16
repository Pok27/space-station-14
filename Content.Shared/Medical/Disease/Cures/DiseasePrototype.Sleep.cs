namespace Content.Shared.Medical.Disease;

[DataDefinition]
public sealed partial class CureSleep : CureStep
{
    /// <summary>
    /// Total required accumulated sleep seconds.
    /// </summary>
    [DataField]
    public float RequiredSleepSeconds { get; private set; } = 0f;

    /// <summary>
    /// Decay rate of sleep accumulation while awake (seconds per second).
    /// </summary>
    [DataField]
    public float AwakeDecayPerSecond { get; private set; } = 0f;
}
