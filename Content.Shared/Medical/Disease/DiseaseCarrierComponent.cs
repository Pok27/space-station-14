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
    /// Prototype IDs the entity is immune to (e.g. via vaccine or recovered).
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> Immunity = new();
}
