using Content.Client.Items.EntitySystems;
using Content.Client.Items.UI;

namespace Content.Client.Items.Components;

/// <summary>
/// Exposes gas tank pressure information via item status control.
/// </summary>
/// <remarks>
/// Shows the tank pressure in kPa and Open/Closed state.
/// </remarks>
/// <seealso cref="TankPressureItemStatusSystem"/>
/// <seealso cref="TankPressureStatusControl"/>
[RegisterComponent]
public sealed partial class TankPressureItemStatusComponent : Component
{
    /// <summary>
    /// The name of the gas tank solution to monitor.
    /// Defaults to "air".
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string TankSolution = "air";
}