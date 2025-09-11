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
    /// Base stealth. Higher = harder to detect early.
    /// </summary>
    [DataField]
    public float Stealth { get; private set; } = 0f;

    /// <summary>
    /// Base resistance. Higher = harder to cure.
    /// </summary>
    [DataField]
    public float Resistance { get; private set; } = 0f;

    /// <summary>
    /// Progression rate factor (stages per minute baseline).
    /// </summary>
    [DataField]
    public float StageSpeed { get; private set; } = 1f;

    /// <summary>
    /// Maximum stages supported by this disease.
    /// </summary>
    [DataField]
    public int MaxStages { get; private set; } = 4;

    /// <summary>
    /// Symptom IDs in activation order.
    /// </summary>
    [DataField]
    public List<ProtoId<DiseaseSymptomPrototype>> Symptoms { get; private set; } = new();

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
