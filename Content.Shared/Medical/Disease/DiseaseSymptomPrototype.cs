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
    /// Minimum stage this symptom begins to be eligible.
    /// </summary>
    [DataField]
    public int MinStage { get; private set; } = 1;

    /// <summary>
    /// Which built-in behavior should be used by the server system.
    /// </summary>
    [DataField]
    public SymptomBehavior Behavior { get; private set; } = SymptomBehavior.None;

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
    /// Optional per-symptom cloud-emitter configuration.
    /// When present, a cloud will be spawned using these parameters when the symptom triggers.
    /// </summary>
    [DataField]
    public SymptomCloudConfig? Cloud { get; private set; }

    /// <summary>
    /// Optional per-symptom airborne spread configuration.
    /// When present, nearby entities can be infected using these parameters (if the disease allows airborne spread).
    /// </summary>
    [DataField]
    public SymptomAirborneConfig? Airborne { get; private set; }
}

/// <summary>
/// Configuration for spawning a transient disease cloud when a symptom triggers.
/// </summary>
[DataDefinition]
public sealed partial class SymptomCloudConfig
{
    /// <summary>
    /// Cloud infection radius in world units.
    /// </summary>
    [DataField]
    public float Range { get; private set; } = 1.5f;

    /// <summary>
    /// Per-target infection probability each tick (0-1).
    /// </summary>
    [DataField]
    public float InfectChance { get; private set; } = 0.1f;

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
public sealed partial class SymptomAirborneConfig
{
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
