using Robust.Shared.GameStates;

namespace Content.Shared.E3D.Components;

/// <summary>
/// Enables pseudo-3D first-person rendering for the local player when attached to their controlled entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FirstPersonViewComponent : Component
{
    /// <summary>
    /// Eye height relative to the entity's map position.
    /// Mirrors Yog e3D's idea of e3d_eye_height, but as content data.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float EyeHeight = 0.82f;

    [DataField, AutoNetworkedField]
    public float FovDegrees = 55f;

    [DataField, AutoNetworkedField]
    public float MaxDistance = 18f;

    [DataField, AutoNetworkedField]
    public bool PitchEnabled = true;

    [DataField, AutoNetworkedField]
    public float InteractionDistance = 2.5f;

    [DataField, AutoNetworkedField]
    public int ColumnStep = 2;

    [DataField, AutoNetworkedField]
    public bool FloorEnabled = true;

    [DataField, AutoNetworkedField]
    public bool BillboardEnabled = true;

    [DataField, AutoNetworkedField]
    public FirstPersonLightingMode LightingMode = FirstPersonLightingMode.DistanceFog;

    [DataField, AutoNetworkedField]
    public FirstPersonQualityPreset QualityPreset = FirstPersonQualityPreset.CorrectnessLow;

    [DataField, AutoNetworkedField]
    public int LogicalColumns = 160;

    [DataField, AutoNetworkedField]
    public int MaxBillboards = 64;

    [DataField, AutoNetworkedField]
    public bool EnableFloorPass = true;
}

