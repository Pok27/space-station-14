namespace Content.Shared.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomJitter : SymptomBehavior
{
    /// <summary>
    /// Duration in seconds for the jitter effect.
    /// </summary>
    [DataField]
    public float JitterSeconds { get; private set; } = 2.0f;

    /// <summary>
    /// Amplitude of jitter movement.
    /// </summary>
    [DataField]
    public float JitterAmplitude { get; private set; } = 6.0f;

    /// <summary>
    /// Frequency of jitter movement.
    /// </summary>
    [DataField]
    public float JitterFrequency { get; private set; } = 3.0f;
}
