using System.Collections.Generic;
using Content.Server.Mech.Components;
using Content.Server.Power.Components;
using Content.Shared.APC;
using Content.Shared.Mech.Components;
using Robust.Shared.GameObjects;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Aggregates recharge contributions from Tesla relay mech modules by detecting powered APCs near mechs
/// and adding their configured chargeRate into the mech's per-tick recharge accumulator.
/// </summary>
public sealed partial class MechTeslaRelaySystem : EntitySystem
{
	[Dependency] private readonly EntityLookupSystem _lookup = default!;

	public override void Update(float frameTime)
	{
		var query = EntityQueryEnumerator<MechComponent>();
		while (query.MoveNext(out var mechUid, out var mech))
		{
			if (!TryComp<MechRechargeAccumulatorComponent>(mechUid, out var acc))
				acc = EnsureComp<MechRechargeAccumulatorComponent>(mechUid);

			foreach (var module in mech.ModuleContainer.ContainedEntities)
			{
				if (!TryComp<MechGeneratorModuleComponent>(module, out var gen))
					continue;
				if (gen.GenerationType != MechGenerationType.TeslaRelay)
					continue;
				if (IsNearPoweredApc(mechUid, gen.Radius))
					acc.PendingRechargeRate += gen.ChargeRate;
			}
		}
	}

	private bool IsNearPoweredApc(EntityUid mech, float radius)
	{
		var apcs = new HashSet<Entity<ApcComponent>>();
		_lookup.GetEntitiesInRange(Transform(mech).Coordinates, radius, apcs);
		foreach (var apc in apcs)
		{
			if (apc.Comp.MainBreakerEnabled && apc.Comp.LastExternalState != ApcExternalPowerState.None)
				return true;
		}
		return false;
	}
}
