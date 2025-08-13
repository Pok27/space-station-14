using Content.Server.Mech.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Content.Server.Power.Components; // ChargerComponent
using Content.Shared.Whitelist;

namespace Content.Server.Mech.Equipment.EntitySystems;
public sealed class MechGunSystem : EntitySystem
{
    [Dependency] private readonly MechSystem _mech = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechEquipmentComponent, GunShotEvent>(MechGunShot);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MechComponent>();
        while (query.MoveNext(out var mechUid, out var mech))
        {
            if (mech.Energy <= 0)
                continue;

            if (!TryComp<ChargerComponent>(mechUid, out var charger))
                continue;

            var rate = charger.ChargeRate;
            if (rate <= 0f)
                continue;

            if (!_containers.TryGetContainer(mechUid, charger.SlotId, out var container))
            {
                container = mech.EquipmentContainer;
                if (container == null)
                    continue;
            }

            foreach (var ent in container.ContainedEntities)
            {
                if (_whitelist.IsWhitelistFail(charger.Whitelist, ent))
                    continue;

                if (!TryComp<BatteryComponent>(ent, out var bat))
                    continue;
                if (bat.CurrentCharge >= bat.MaxCharge)
                    continue;

                var toAdd = MathF.Min(rate * (float) frameTime, bat.MaxCharge - bat.CurrentCharge);
                toAdd = MathF.Min(toAdd, mech.Energy.Float());
                if (toAdd <= 0)
                    continue;

                if (!_mech.TryChangeEnergy(mechUid, -toAdd, mech))
                    continue;

                _battery.SetCharge(ent, bat.CurrentCharge + toAdd, bat);
            }
        }
    }

    private void MechGunShot(EntityUid uid, MechEquipmentComponent component, ref GunShotEvent args)
    {
        // No-op: passive charging handled in Update
    }
}
