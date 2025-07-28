using Content.Client.Items.EntitySystems;
using Content.Client.Items.UI;

namespace Content.Client.Items.Components;

/// <summary>
/// Exposes limited charges information via item status control.
/// </summary>
/// <remarks>
/// Shows the current charges out of maximum charges.
/// </remarks>
/// <seealso cref="ChargeItemStatusSystem"/>
/// <seealso cref="ChargeStatusControl"/>
[RegisterComponent]
public sealed partial class ChargeItemStatusComponent : Component
{
    /// <summary>
    /// Optional descriptive name for what the charges represent (e.g., "charges", "uses", "shots").
    /// If not set, defaults to "charges".
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string ChargeName = "charges";

    /// <summary>
    /// Whether to show a recovery timer if auto-recharge is available.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool ShowRechargeTimer = true;
}