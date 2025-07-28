using Content.Client.Items.EntitySystems;
using Content.Client.Items.UI;

namespace Content.Client.Items.Components;

/// <summary>
/// Exposes a battery's charge information via item status control.
/// </summary>
/// <remarks>
/// Shows the battery charge percentage and On/Off state if applicable.
/// </remarks>
/// <seealso cref="BatteryItemStatusSystem"/>
/// <seealso cref="BatteryStatusControl"/>
[RegisterComponent]
public sealed partial class BatteryItemStatusComponent : Component
{
    /// <summary>
    /// Whether to show On/Off toggle state for this battery-powered item.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool ShowToggleState = false;
}