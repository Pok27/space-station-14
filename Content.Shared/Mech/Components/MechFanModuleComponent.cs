using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Mech.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechFanModuleComponent : Component
{
    /// <summary>
    /// Whether the fan is currently active
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool IsActive = false;

    /// <summary>
    /// Current fan state (Off, On, Idle)
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public MechFanState State = MechFanState.Off;

    /// <summary>
    /// How much energy the fan consumes per second when active
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 EnergyConsumption = 1.0f;

    /// <summary>
    /// How much gas the fan can process per second when active
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GasProcessingRate = 1f;

    /// <summary>
    /// Whether the attached filter (if any) should be active
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool FilterEnabled = true;
}
