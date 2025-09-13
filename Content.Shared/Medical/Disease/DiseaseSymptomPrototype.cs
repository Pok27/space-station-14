using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Disease;

/// <summary>
/// Prototype for defining disease symptoms via YAML.
/// Logic is handled by server systems.
/// </summary>
[Prototype("diseaseSymptom")]
public sealed partial class DiseaseSymptomPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Behavior variants configured by name. Each entry has a type name and inline parameters.
    /// </summary>
    [DataField]
    public List<SymptomBehavior> Behaviors { get; private set; } = new();

    /// <summary>
    /// Probability per tick to trigger behavior when eligible (0-1).
    /// </summary>
    [DataField("triggerProb")]
    public float TriggerProbability { get; private set; } = 0f;

    /// <summary>
    /// Optional status effect to apply on trigger (e.g. jitter, slowed).
    /// </summary>
    [DataField]
    public ProtoId<StatusEffectPrototype>? StatusEffect { get; private set; }

    /// <summary>
    /// Configuration for cloud.
    /// </summary>
    [DataField]
    public SymptomCloud Cloud { get; private set; } = new();

    /// <summary>
    /// Configuration for airborne spread.
    /// </summary>
    [DataField]
    public SymptomAirborne Airborne { get; private set; } = new();

    /// <summary>
    /// Configuration for leaving residue.
    /// </summary>
    [DataField]
    public SymptomLeaveResidue LeaveResidue { get; private set; } = new();

    /// <summary>
    /// How long (seconds) a successful symptom-level cure should suppress this symptom.
    /// If zero, symptom-level cures do not suppress.
    /// </summary>
    [DataField]
    public float CureDuration { get; private set; } = 0f;

    /// <summary>
    /// Optional cure steps specific to this symptom. These will be attempted by the
    /// server cure system and, if succeeded, will suppress the symptom for
    /// <see cref="CureDuration"/> instead of curing the underlying disease.
    /// </summary>
    [DataField]
    public List<CureStep> CureSteps { get; private set; } = new();
}

/// <summary>
/// Configuration for spawning a transient disease cloud when a symptom triggers.
/// </summary>
[DataDefinition]
public sealed partial class SymptomCloud
{
    /// <summary>
    ///
    /// </summary>
    [DataField]
    public bool Enabled { get; private set; } = true;

    /// <summary>
    /// Cloud infection radius in world units.
    /// </summary>
    [DataField]
    public float Range { get; private set; } = 1.5f;

    /// <summary>
    /// Tick period in seconds for the cloud infection attempts.
    /// </summary>
    [DataField("tickInterval")]
    public float TickIntervalSeconds { get; private set; } = 1.0f;

    /// <summary>
    /// Lifetime in seconds before the cloud expires.
    /// </summary>
    [DataField("lifetime")]
    public float LifetimeSeconds { get; private set; } = 8.0f;
}

/// <summary>
/// Configuration for symptom-driven airborne spread.
/// </summary>
[DataDefinition]
public sealed partial class SymptomAirborne
{
    /// <summary>
    ///
    /// </summary>
    [DataField]
    public bool Enabled { get; private set; } = true;

    /// <summary>
    /// Airborne infection radius in world units.
    /// </summary>
    [DataField]
    public float Range { get; private set; } = 1.5f;

    /// <summary>
    /// Base per-target infection probability (0-1) before PPE adjustments.
    /// </summary>
    [DataField]
    public float BaseChance { get; private set; } = 0.25f;
}

[DataDefinition]
public sealed partial class SymptomLeaveResidue
{
    /// <summary>
    ///
    /// </summary>
    [DataField]
    public bool Enabled { get; private set; } = true;

    /// <summary>
    ///
    /// </summary>
    [DataField]
    public float ResidueIntensity { get; private set; } = 0.5f;
}
