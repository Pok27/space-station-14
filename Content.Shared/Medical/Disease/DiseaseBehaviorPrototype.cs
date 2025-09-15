using System.Collections.Generic;
using Content.Shared.Damage;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Content.Shared.Popups;

namespace Content.Shared.Medical.Disease;

/// <summary>
/// Base class for symptom behavior configurations.
/// </summary>
public abstract partial class SymptomBehavior
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
}

[DataDefinition]
public sealed partial class SymptomVomit : SymptomBehavior
{
}

[DataDefinition]
public sealed partial class SymptomJitter : SymptomBehavior
{
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
}

[DataDefinition]
public sealed partial class SymptomTemperature : SymptomBehavior
{
    /// <summary>
    /// Target temperature (Celsius) the symptom will try to move the entity towards.
    /// </summary>
    [DataField]
    public float TargetTemperature { get; private set; } = 37.0f;

    /// <summary>
    /// How many degrees per second we attempt to move the entity's body temperature towards the target.
    /// Note: the implementation converts this into thermal energy using the entity heat capacity.
    /// </summary>
    [DataField]
    public float DegreesPerSecond { get; private set; } = 0.5f;
}

[DataDefinition]
public sealed partial class SymptomNarcolepsy : SymptomBehavior
{
    /// <summary>
    /// Frequency (per-symptom-trigger) chance to fall asleep when this behavior runs (0-1).
    /// The probability is checked in the symptom trigger flow in addition to disease triggerProb.
    /// </summary>
    [DataField]
    public float SleepChance { get; private set; } = 0.5f;

    /// <summary>
    /// How long (seconds) the forced sleep should last when applied.
    /// </summary>
    [DataField("sleepDuration")]
    public float SleepDurationSeconds { get; private set; } = 5.0f;
}

/// <summary>
/// Deals configurable damage to the carrier when triggered.
/// Supports multiple damage types via DamageSpecifier and an optional minimum interval between applications.
/// </summary>
[DataDefinition]
public sealed partial class SymptomDamage : SymptomBehavior
{
    /// <summary>
    /// Damage to apply, potentially across multiple types.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage { get; private set; } = new();
}

/// <summary>
/// Forces the carrier to shout random text when triggered. Uses a localized dataset if provided.
/// </summary>
[DataDefinition]
public sealed partial class SymptomShout : SymptomBehavior
{
    /// <summary>
    /// Optional dataset of localized lines to shout. A random entry will be selected.
    /// If not provided, falls back to a simple "!".
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? Pack { get; private set; }

    /// <summary>
    /// If true, hide the message from the chat window (bubble only).
    /// </summary>
    [DataField]
    public bool HideChat { get; private set; } = true;
}

/// <summary>
/// Shows a private popup to the carrier with a localized message.
/// </summary>
[DataDefinition]
public sealed partial class SymptomSensation : SymptomBehavior
{
    /// <summary>
    /// Localization key for the popup message.
    /// </summary>
    [DataField]
    public string Popup { get; private set; } = string.Empty;

    /// <summary>
    /// Popup visual type.
    /// </summary>
    [DataField]
    public PopupType PopupType { get; private set; } = PopupType.Small;
}
