using Robust.Shared.GameStates;

namespace Content.Shared.Power;

/// <summary>
/// Raised to get battery charge information for UI purposes.
/// </summary>
[ByRefEvent]
public sealed class GetBatteryInfoEvent : EntityEventArgs
{
    /// <summary>
    /// Current charge of the battery (0-1 as percentage).
    /// </summary>
    public float ChargePercent;

    /// <summary>
    /// Whether the battery exists and has valid data.
    /// </summary>
    public bool HasBattery;
}

/// <summary>
/// Shared component to hold networked battery state information for UI display.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SharedBatteryStateComponent : Component
{
    /// <summary>
    /// Current charge percentage (0-1).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ChargePercent;

    /// <summary>
    /// Whether the battery exists and has valid data.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HasBattery;
}
