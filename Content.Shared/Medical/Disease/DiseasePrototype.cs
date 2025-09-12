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
    /// Spread vectors for this disease.
    /// </summary>
    [DataField]
    public DiseaseSpreadFlags SpreadFlags { get; private set; } = DiseaseSpreadFlags.None;

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
}
