using Robust.Shared.GameStates;

namespace Content.Shared.E3D.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FirstPersonViewComponent : Component
{
    [DataField, AutoNetworkedField]
    public float EyeHeight = FirstPersonViewDefaults.DefaultEyeHeight;

    [DataField, AutoNetworkedField]
    public float FovDegrees = FirstPersonViewDefaults.DefaultFovDegrees;

    [DataField, AutoNetworkedField]
    public float MaxDistance = FirstPersonViewDefaults.DefaultMaxDistance;

    [DataField, AutoNetworkedField]
    public bool PitchEnabled = FirstPersonViewDefaults.DefaultPitchEnabled;

    [DataField, AutoNetworkedField]
    public float InteractionDistance = FirstPersonViewDefaults.DefaultInteractionDistance;

    [DataField, AutoNetworkedField]
    public int ColumnStep = FirstPersonViewDefaults.DefaultColumnStep;

    [DataField, AutoNetworkedField]
    public bool FloorEnabled = FirstPersonViewDefaults.DefaultFloorEnabled;

    [DataField, AutoNetworkedField]
    public bool BillboardEnabled = FirstPersonViewDefaults.DefaultBillboardEnabled;

    [DataField, AutoNetworkedField]
    public FirstPersonLightingMode LightingMode = FirstPersonViewDefaults.DefaultLightingMode;

    [DataField, AutoNetworkedField]
    public FirstPersonQualityPreset QualityPreset = FirstPersonViewDefaults.DefaultQualityPreset;

    [DataField, AutoNetworkedField]
    public int LogicalColumns = FirstPersonViewDefaults.DefaultLogicalColumns;

    [DataField, AutoNetworkedField]
    public int MaxBillboards = FirstPersonViewDefaults.DefaultMaxBillboards;

    [DataField, AutoNetworkedField]
    public bool EnableFloorPass = FirstPersonViewDefaults.DefaultEnableFloorPass;
}

public static class FirstPersonViewDefaults
{
    public const float DefaultEyeHeight = 0.82f;
    public const float DefaultFovDegrees = 55f;
    public const float DefaultMaxDistance = 18f;
    public const bool DefaultPitchEnabled = true;
    public const float DefaultInteractionDistance = 2.5f;
    public const int DefaultColumnStep = 2;
    public const bool DefaultFloorEnabled = true;
    public const bool DefaultBillboardEnabled = true;
    public const FirstPersonLightingMode DefaultLightingMode = FirstPersonLightingMode.DistanceFog;
    public const FirstPersonQualityPreset DefaultQualityPreset = FirstPersonQualityPreset.CorrectnessLow;
    public const int DefaultLogicalColumns = 160;
    public const int DefaultMaxBillboards = 64;
    public const bool DefaultEnableFloorPass = true;
}

