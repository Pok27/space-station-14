namespace Content.Shared.Medical.Disease;

/// <summary>
/// Base class for symptom behavior configurations.
/// </summary>
[DataDefinition]
public partial class SymptomBehavior
{
}

[DataDefinition]
public sealed partial class SymptomExhale : SymptomBehavior
{
    /// <summary>
    /// Localization key for popup text.
    /// </summary>
    [DataField]
    public string PopupText { get; private set; } = "disease-cough";

    /// <summary>
    /// Sound volume for behavior SFX.
    /// </summary>
    [DataField]
    public float SoundVolume { get; private set; } = -2f;

    /// <summary>
    /// Sound variation for behavior SFX.
    /// </summary>
    [DataField]
    public float SoundVariation { get; private set; } = 0.1f;

    /// <summary>
    /// Jitter duration in seconds.
    /// </summary>
    [DataField]
    public float JitterSeconds { get; private set; } = 2.0f;

    /// <summary>
    /// Jitter amplitude.
    /// </summary>
    [DataField]
    public float JitterAmplitude { get; private set; } = 6.0f;

    /// <summary>
    /// Jitter frequency.
    /// </summary>
    [DataField]
    public float JitterFrequency { get; private set; } = 3.0f;

    /// <summary>
    /// Residue intensity left after the behavior.
    /// </summary>
    [DataField]
    public float ResidueIntensity { get; private set; } = 0.5f;
}

[DataDefinition]
public sealed partial class SymptomVomit : SymptomBehavior
{
}

[DataDefinition]
public sealed partial class SymptomFever : SymptomBehavior
{
}
