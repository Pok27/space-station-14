using Robust.Shared.GameObjects;

namespace Content.Server.Mech.Components;

/// <summary>
///
/// </summary>
[RegisterComponent]
public sealed partial class MechRechargeAccumulatorComponent : Component
{
    /// <summary>
    ///
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float PendingRechargeRate;

    /// <summary>
    ///
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
	public float Current;

    /// <summary>
    ///
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
	public float Max;
}
