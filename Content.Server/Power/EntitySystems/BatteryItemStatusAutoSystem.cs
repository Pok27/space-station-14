using Content.Shared.Power.Components;
using Content.Server.Power.Components;
using Content.Shared.PowerCell.Components;
using Content.Shared.Item;
using Content.Shared.Power;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.Power.EntitySystems;

/// <summary>
/// Automatically adds SharedBatteryItemComponent to items that have batteries,
/// making battery status visible when examining items.
/// Also handles syncing battery charge information from server to client.
/// </summary>
public sealed class BatteryItemStatusAutoSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        
        // Only need to handle MapInitEvent for ItemComponent - this covers all cases
        SubscribeLocalEvent<ItemComponent, MapInitEvent>(OnItemMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Update battery charge information for all items with SharedBatteryItemComponent
        var enumerator = EntityQueryEnumerator<SharedBatteryItemComponent>();
        while (enumerator.MoveNext(out var uid, out var batteryItem))
        {
            // Use GetBatteryInfoEvent to check if item has battery and get charge
            var infoEvent = new GetBatteryInfoEvent();
            RaiseLocalEvent(uid, ref infoEvent);

            // If no battery found, remove the component (shouldn't happen normally)
            if (!infoEvent.HasBattery)
            {
                RemComp<SharedBatteryItemComponent>(uid);
                continue;
            }

            int percent = (int)(infoEvent.ChargePercent * 100);

            if (percent != batteryItem.ChargePercent)
            {
                batteryItem.ChargePercent = percent;
                Dirty(uid, batteryItem);
            }
        }
    }

    private void OnItemMapInit(EntityUid uid, ItemComponent component, MapInitEvent args)
    {
        // Only add to items that don't already have the component
        if (HasComp<SharedBatteryItemComponent>(uid))
            return;

        // Don't add if item has AmmoCounterComponent (weapons show ammo, not battery)
        if (HasComp<SharedAmmoCounterComponent>(uid))
            return;

        // Use GetBatteryInfoEvent to check if item has a battery
        var infoEvent = new GetBatteryInfoEvent();
        RaiseLocalEvent(uid, ref infoEvent);

        // If has battery, add the component
        if (infoEvent.HasBattery)
        {
            EnsureComp<SharedBatteryItemComponent>(uid);
        }
    }
}