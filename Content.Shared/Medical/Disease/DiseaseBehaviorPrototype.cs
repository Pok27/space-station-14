using System.Collections.Generic;
using Content.Shared.Damage;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;

namespace Content.Shared.Medical.Disease;

/// <summary>
/// Base class for symptom behavior configurations.
/// </summary>
public abstract partial class SymptomBehavior
{
}

[DataDefinition]
public sealed partial class SymptomEmote : SymptomBehavior
{
    /// <summary>
    /// Localization key for popup text.
    /// </summary>
    [DataField]
    public string? PopupText { get; private set; }

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
    /// Optional override for the sound to play when exhaling.
    /// If not provided, defaults to gendered cough sounds.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound { get; private set; }
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
    /// Target temperature (Kelvin) the symptom will try to move the entity towards.
    /// </summary>
    [DataField]
    public float TargetTemperature { get; private set; } = 310.15f;

    /// <summary>
    /// Temperature change step (Kelvin per symptom trigger).
    /// The implementation converts the delta into thermal energy using entity heat capacity.
    /// </summary>
    [DataField]
    public float StepTemperature { get; private set; } = 0.5f;
}

[DataDefinition]
public sealed partial class SymptomNarcolepsy : SymptomBehavior
{
    /// <summary>
    /// Frequency (per-symptom-trigger) chance to fall asleep when this behavior runs (0-1).
    /// The probability is checked in the symptom trigger flow in addition to disease triggerProb.
    /// </summary>
    [DataField]
    public float SleepChance { get; private set; } = 0.6f;

    /// <summary>
    /// How long (seconds) the forced sleep should last when applied.
    /// </summary>
    [DataField("sleepDuration")]
    public float SleepDurationSeconds { get; private set; } = 6.0f;
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

/// <summary>
/// Applies or edits a status effect on the carrier using the legacy string-key StatusEffects system.
/// Use with keys defined in status_effects.yml. Optional component name attaches a component while effect lasts.
/// </summary>
[DataDefinition]
public sealed partial class SymptomGenericStatusEffect : SymptomBehavior
{
    /// <summary>
    /// Status effect key.
    /// </summary>
    [DataField(required: true)]
    public string Key { get; private set; } = string.Empty;

    /// <summary>
    /// Optional component to ensure on target when effect is applied.
    /// </summary>
    [DataField]
    public string Component { get; private set; } = string.Empty;

    /// <summary>
    /// Seconds to add/remove/set on the effect cooldown. Clamped minimally above 0 when used.
    /// </summary>
    [DataField]
    public float TimeSeconds { get; private set; } = 1.0f;

    /// <summary>
    /// If true, refresh effect time instead of accumulating when adding.
    /// </summary>
    [DataField]
    public bool Refresh { get; private set; } = false;

    /// <summary>
    /// Operation type: Add, Remove (subtract time), or Set (replace remaining time).
    /// </summary>
    [DataField]
    public StatusEffectApplyType Type { get; private set; } = StatusEffectApplyType.Add;
}

public enum StatusEffectApplyType
{
    Add,
    Remove,
    Set
}

/// <summary>
/// Adds a component to the carrier if missing.
/// </summary>
[DataDefinition]
public sealed partial class SymptomAddComponent : SymptomBehavior
{
    /// <summary>
    /// Component registration name to add to the carrier.
    /// </summary>
    [DataField(required: true)]
    public string Component { get; private set; } = string.Empty;
}

/// <summary>
/// Transitions the current disease to another disease prototype when triggered.
/// </summary>
[DataDefinition]
public sealed partial class SymptomTransitionDisease : SymptomBehavior
{
    /// <summary>
    /// Target disease prototype ID to transition into.
    /// </summary>
    [DataField(required: true)]
    public string Disease { get; private set; } = string.Empty;

    /// <summary>
    /// Optional stage to start the new disease from.
    /// </summary>
    [DataField]
    public int StartStage { get; private set; } = 1;
}
