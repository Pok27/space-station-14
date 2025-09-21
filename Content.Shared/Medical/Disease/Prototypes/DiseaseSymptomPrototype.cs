using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;

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
    /// Behavior variants configured by name. Each entry is a symptom effect with its own parameters.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<SymptomBehavior> Behaviors { get; private set; } = new();

    /// <summary>
    /// Probability per tick to trigger behavior when eligible (0-1).
    /// </summary>
    [DataField]
    public float Probability { get; private set; } = 0.05f;

    /// <summary>
    /// If true, only a single randomly selected behavior from <see cref="Behaviors"/> will run when the symptom triggers.
    /// </summary>
    [DataField]
    public bool SingleBehavior { get; private set; } = false;

    /// <summary>
    /// If true, this symptom will only trigger on living carriers. If the carrier is dead the symptom is skipped.
    /// </summary>
    [DataField]
    public bool OnlyWhenAlive { get; private set; } = false;

    /// <summary>
    /// Configuration for symptom-driven airborne burst.
    /// </summary>
    [DataField]
    public SymptomAirborneBurst AirborneBurst { get; private set; } = new();

    /// <summary>
    /// How long (seconds) a successful symptom-level cure should suppress this symptom.
    /// If zero, symptom-level cures do not suppress.
    /// </summary>
    [DataField]
    public float CureDuration { get; private set; } = 0f;

    /// <summary>
    /// Optional cure steps specific to this symptom. These are attempted by the cure system and, on success,
    /// suppress this symptom for <see cref="CureDuration"/> instead of curing the disease.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<CureStep> CureSteps { get; private set; } = new();
}

[DataDefinition]
public sealed partial class SymptomAirborneBurst
{
    /// <summary>
    /// Multiplier to disease airborne range for this burst.
    /// </summary>
    [DataField]
    public float RangeMultiplier { get; private set; } = 1.0f;

    /// <summary>
    /// Multiplier to disease airborne infection chance for this burst.
    /// </summary>
    [DataField]
    public float ChanceMultiplier { get; private set; } = 1.0f;
}

/// <summary>
/// Base class for symptom behavior.
/// </summary>
public abstract partial class SymptomBehavior
{
    /// <summary>
    /// Called when the symptom is triggered on the carrier.
    /// </summary>
    public virtual void OnSymptom(EntityUid uid, DiseasePrototype disease)
    {
    }

    /// <summary>
    /// Called when the parent disease is fully cured on the carrier.
    /// </summary>
    public virtual void OnDiseaseCured(EntityUid uid, DiseasePrototype disease)
    {
    }

    /// <summary>
    /// Called when this symptom is cured/suppressed on the carrier.
    /// </summary>
    public virtual void OnSymptomCured(EntityUid uid, DiseasePrototype disease, string symptomId)
    {
    }
}
