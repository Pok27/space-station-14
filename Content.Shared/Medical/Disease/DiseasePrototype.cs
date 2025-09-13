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
    /// Progression rate factor (stages per minute baseline).
    /// </summary>
    [DataField]
    public float StageSpeed { get; private set; } = 1f;

    /// <summary>
    /// Stage configurations in ascending order (1-indexed semantics). Each stage can define stealth/resistance and symptom activations.
    /// </summary>
    [DataField]
    public List<DiseaseStage> Stages { get; private set; } = new();

    /// <summary>
    /// Optional list of cure steps for the disease. Mirrors the `behaviors` pattern used by symptoms:
    /// a typed list where each entry can be a variant like `CureReagent`.
    /// </summary>
    [DataField]
    public List<CureStep> CureSteps { get; private set; } = new();

    /// <summary>
    /// Default immunity strength granted after curing this disease (0-1).
    /// When a carrier is cured, this value is written into the carrier's immunity map unless
    /// the disease stage-specific cure step overrides it.
    /// </summary>
    [DataField]
    public float PostCureImmunityStrength { get; private set; } = 0.8f;

    /// <summary>
    /// Spread vectors for this disease. Use a list so multiple vectors can be selected in prototypes.
    /// Example YAML: spreadFlags: [Airborne, Contact]
    /// </summary>
    [DataField]
    public List<DiseaseSpreadFlags> SpreadFlags { get; private set; } = new();

    /// <summary>
    /// If true, masks (mask slot PPE) will not reduce airborne infection chance for this disease.
    /// </summary>
    [DataField]
    public bool IgnoreMaskPPE { get; private set; } = false;

    /// <summary>
    /// Severity band for administrative/UX use.
    /// </summary>
    [DataField]
    public DiseaseSeverity Severity { get; private set; } = DiseaseSeverity.Minor;
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
    /// Optional stealth value for this stage. Higher = harder to detect.
    /// </summary>
    [DataField]
    public float? Stealth { get; private set; }

    /// <summary>
    /// Optional resistance value for this stage. Higher = harder to cure.
    /// </summary>
    [DataField]
    public float? Resistance { get; private set; }

    /// <summary>
    /// Symptoms that can trigger during this stage. Order matters for deterministic iteration.
    /// Each entry is a symptom prototype ID. No per-disease overrides here.
    /// </summary>
    [DataField]
    public List<ProtoId<DiseaseSymptomPrototype>> Symptoms { get; private set; } = new();

    /// <summary>
    /// Optional list of cure steps specific to this stage. If present it overrides the disease-level `CureSteps` for this stage.
    /// </summary>
    [DataField]
    public List<CureStep> CureSteps { get; private set; } = new();
}
