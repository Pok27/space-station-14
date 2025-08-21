using Robust.Shared.GameObjects;

namespace Content.Server.Mech.Components;

/// <summary>
/// Per-mech accumulator used to aggregate recharge rates from multiple generator systems within a single update tick.
/// The total is applied to the mech battery by a dedicated apply system and then reset.
/// </summary>
[RegisterComponent]
public sealed partial class MechRechargeAccumulatorComponent : Component
{
	[ViewVariables(VVAccess.ReadWrite)]
	public float PendingRechargeRate;
}
