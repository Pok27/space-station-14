using Content.Client.Magazine.EntitySystems;
using Content.Client.Magazine.UI;

namespace Content.Client.Magazine.Components;

/// <summary>
/// Exposes magazine ammunition information via item status control.
/// </summary>
/// <remarks>
/// Shows the current rounds out of maximum capacity.
/// </remarks>
/// <seealso cref="MagazineItemStatusSystem"/>
/// <seealso cref="MagazineStatusControl"/>
[RegisterComponent]
public sealed partial class MagazineItemStatusComponent : Component
{
    /// <summary>
    /// Optional descriptive name for what the ammunition represents (e.g., "rounds", "shells", "shots").
    /// If not set, defaults to "rounds".
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string AmmoName = "magazine-status-name-rounds";
}