using Robust.Shared.GameStates;

namespace Content.Shared.E3D.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FirstPersonViewComponent : Component
{
    [DataField, AutoNetworkedField]
    public float EyeHeight = 0.82f;

    [DataField, AutoNetworkedField]
    public float FovDegrees = 100f;

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
    public int MaxBillboards = 16;

    [DataField, AutoNetworkedField]
    public bool EnableFloorPass = true;
}

