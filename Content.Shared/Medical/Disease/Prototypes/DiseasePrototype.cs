using System.Collections.Generic;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Disease;

/// <summary>
/// Prototype for defining diseases via YAML.
/// Logic is handled by server systems.
/// </summary>
[Prototype("disease")]
public sealed partial class DiseasePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Display name for UIs.
    /// </summary>
    [DataField]
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// If true, this disease is considered beneficial for HUD purposes.
    /// Beneficial diseases show a buff icon on med HUD instead of an illness icon.
    /// </summary>
    [DataField]
    public bool IsBeneficial { get; private set; } = false;

    /// <summary>
    /// Speed of progression through disease stages per second.
    /// </summary>
    [DataField]
    public float StageSpeed { get; private set; } = 0.2f;

    /// <summary>
    /// Stage configurations in ascending order (1-indexed semantics). Each stage can define stealth/resistance and symptom activations.
    /// </summary>
    [DataField]
    public List<DiseaseStage> Stages { get; private set; } = new();

    /// <summary>
    /// Optional list of cure steps for the disease. Each entry is a specific cure action (e.g., reagent, time).
    /// </summary>
    [DataField(serverOnly: true)]
    public List<CureStep> CureSteps { get; private set; } = new();

    /// <summary>
    /// Default immunity strength granted after curing this disease (0-1).
    /// When a carrier is cured, this value is written into the carrier's immunity map unless
    /// the disease stage-specific cure step overrides it.
    /// </summary>
    [DataField]
    public float PostCureImmunity { get; private set; } = 0.7f;

    /// <summary>
    /// Spread vectors for this disease. Use a list so multiple vectors can be selected in prototypes.
    /// </summary>
    [DataField]
    public List<DiseaseSpreadFlags> SpreadFlags { get; private set; } = new();

    /// <summary>
    /// If true, masks (mask slot PPE) will not reduce airborne infection chance for this disease.
    /// </summary>
    [DataField]
    public bool IgnoreMaskPPE { get; private set; } = false;

    /// <summary>
    /// Base per-contact infection probability for this disease (0-1). Used when two entities make contact.
    /// </summary>
    [DataField]
    public float ContactInfect { get; private set; } = 0.01f;

    /// <summary>
    /// Amount of residue intensity deposited when a carrier with this disease contacts a surface.
    /// Expressed as [0..1] fraction added to per-disease residue intensity.
    /// </summary>
    [DataField]
    public float ContactDeposit { get; private set; } = 0.1f;

    /// <summary>
    /// Base per-target airborne infection probability (0-1) before PPE adjustments.
    /// This value is used both for direct airborne spread and for transient clouds spawned by symptoms.
    /// </summary>
    [DataField]
    public float AirborneInfect { get; private set; } = 0.2f;

    /// <summary>
    /// Airborne infection radius in world units, used when <see cref="SpreadFlags"/> contains Airborne.
    /// </summary>
    [DataField]
    public float AirborneRange { get; private set; } = 1.5f;

    /// <summary>
    /// Per-tick chance (0-1) to attempt airborne spread from each carrier of this disease.
    /// </summary>
    [DataField]
    public float AirborneTickChance { get; private set; } = 0.15f;

    /// <summary>
    /// Optional incubation time in seconds before symptoms/spread begin after infection.
    /// </summary>
    [DataField]
    public float IncubationSeconds { get; private set; } = 0f;
}

/// <summary>
/// Per-stage configuration for a disease. Defines stealth/resistance modifiers and which symptoms become active.
/// </summary>
[DataDefinition]
public sealed partial class DiseaseStage
{
    /// <summary>
    /// Stage number (1-indexed).
    /// </summary>
    [DataField(required: true)]
    public int Stage { get; private set; } = 1;

    /// <summary>
    /// Optional stealth flags for this stage. Controls visibility in HUD/diagnoser/analyzers.
    /// TODO: does not work <see cref="DiseaseStealthFlags"/>
    /// </summary>
    [DataField]
    public DiseaseStealthFlags Stealth { get; private set; } = DiseaseStealthFlags.None;

    /// <summary>
    /// Symptoms that can trigger during this stage. Order matters for deterministic iteration.
    /// Each entry is a mapping with `symptom` and optional `probability` to override the symptom prototype's `probability`.
    /// </summary>
    [DataField]
    public List<SymptomEntry> Symptoms { get; private set; } = new();

    /// <summary>
    /// Optional list of localized message keys to show as "sensations" to the carrier while at this stage.
    /// A single entry is randomly picked on each eligible tick, controlled by <see cref="SensationProb"/>.
    /// </summary>
    [DataField]
    public List<string> Sensation { get; private set; } = new();

    /// <summary>
    /// Per-tick probability (0-1) to show a random sensation popup from <see cref="Sensation"/>.
    /// </summary>
    [DataField]
    public float SensationProb { get; private set; } = 0.05f;

    /// <summary>
    /// Optional list of cure steps specific to this stage. Overrides disease-level <see cref="CureSteps"/> for this stage.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<CureStep> CureSteps { get; private set; } = new();
}

[DataDefinition]
public sealed partial class SymptomEntry
{
    /// <summary>
    /// Symptom prototype ID to trigger.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<DiseaseSymptomPrototype> Symptom { get; private set; } = default!;

    /// <summary>
    /// Per-tick probability (0-1) to trigger this symptom while in the stage. If negative, the probability of symptom is used.
    /// </summary>
    [DataField]
    public float Probability { get; private set; } = -1f;
}
