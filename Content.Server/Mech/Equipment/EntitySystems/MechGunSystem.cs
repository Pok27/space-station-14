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

            var chargeRate = charger.ChargeRate;
            if (chargeRate <= 0f)
                continue;

            // Get the container to charge
            var container = _containers.TryGetContainer(mechUid, charger.SlotId, out var cont)
                ? cont
                : mech.EquipmentContainer;

            if (container == null)
                continue;

            // Charge all weapons in the container
            foreach (var weapon in container.ContainedEntities)
            {
                if (_whitelist.IsWhitelistFail(charger.Whitelist, weapon))
                    continue;

                if (!TryComp<BatteryComponent>(weapon, out var battery))
                    continue;

                if (battery.CurrentCharge >= battery.MaxCharge)
                    continue;

                var chargeNeeded = battery.MaxCharge - battery.CurrentCharge;
                var chargeAvailable = mech.Energy.Float();
                var chargeToAdd = MathF.Min(MathF.Min(chargeRate, chargeNeeded), chargeAvailable);

                if (chargeToAdd <= 0)
                    continue;

                if (_mech.TryChangeEnergy(mechUid, -chargeToAdd, mech))
                {
                    _battery.SetCharge(weapon, battery.CurrentCharge + chargeToAdd, battery);
                }
            }
        }
    }

    private void MechGunShot(EntityUid uid, MechEquipmentComponent component, ref GunShotEvent args)
    {
        // No-op: passive charging handled in Update
    }
}
