using System;
using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Medical.Disease;

/// <summary>
/// Networked component storing active diseases and immunity tokens.
/// Server drives logic; client uses it for UI and visuals.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseCarrierComponent : Component
{
    /// <summary>
    /// Active diseases and their current stage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, int> ActiveDiseases = new();

    /// <summary>
    /// Time when the next disease processing tick occurs.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextTick;

    /// <summary>
    /// Prototype IDs the entity is immune to and their immunity strength (0-1).
    /// Value represents the probability to block infection attempts for that disease.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, float> Immunity = new();

    /// <summary>
    /// Map of symptom prototype IDs to a suppression end time. Used to temporarily
    /// suppress (treat) symptoms without curing the underlying disease.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, TimeSpan> SuppressedSymptoms = new();

    /// <summary>
    /// Server-side: track components that were added by a disease so that cures can roll them back safely.
    /// Key: disease prototype ID, Value: set of component registration names added by that disease.
    /// Not networked.
    /// </summary>
    [DataField]
    public Dictionary<string, HashSet<string>> AddedComponents = new();
}
