using Content.Shared.Power.Components;
using Content.Server.Power.Components;
using Content.Shared.PowerCell.Components;
using Content.Shared.Item;
using Content.Shared.Power;
using Robust.Shared.Containers;

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
        
        // Add battery status component when BatteryComponent is properly initialized
        SubscribeLocalEvent<BatteryComponent, MapInitEvent>(OnBatteryMapInit);
        
        // Add battery status component when PowerCellSlotComponent is properly initialized
        SubscribeLocalEvent<PowerCellSlotComponent, MapInitEvent>(OnPowerCellSlotMapInit);
        
        // Also handle when ItemComponent is added to entities that already have batteries
        SubscribeLocalEvent<ItemComponent, MapInitEvent>(OnItemMapInit);
        
        // Handle power cell insertion into slots
        SubscribeLocalEvent<PowerCellSlotComponent, EntInsertedIntoContainerMessage>(OnCellInserted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Update battery charge information for all items with SharedBatteryItemComponent
        var enumerator = EntityQueryEnumerator<SharedBatteryItemComponent>();
        while (enumerator.MoveNext(out var uid, out var batteryItem))
        {
            // Retrieve battery info using the existing GetBatteryInfoEvent infrastructure
            var infoEvent = new GetBatteryInfoEvent();
            RaiseLocalEvent(uid, ref infoEvent);

            if (!infoEvent.HasBattery)
                continue;

            int percent = (int)(infoEvent.ChargePercent * 100);

            if (percent != batteryItem.ChargePercent)
            {
                batteryItem.ChargePercent = percent;
                Dirty(uid, batteryItem);
            }
        }
    }

    private void OnBatteryMapInit(EntityUid uid, BatteryComponent component, MapInitEvent args)
    {
        TryAddBatteryStatus(uid);
    }

    private void OnPowerCellSlotMapInit(EntityUid uid, PowerCellSlotComponent component, MapInitEvent args)
    {
        TryAddBatteryStatus(uid);
    }

    private void OnItemMapInit(EntityUid uid, ItemComponent component, MapInitEvent args)
    {
        TryAddBatteryStatus(uid);
    }

    private void OnCellInserted(EntityUid uid, PowerCellSlotComponent component, EntInsertedIntoContainerMessage args)
    {
        // When a power cell is inserted, ensure the host has battery status
        TryAddBatteryStatus(uid);
    }

    private void TryAddBatteryStatus(EntityUid uid)
    {
        // Only add to items (things that can be examined)
        if (!HasComp<ItemComponent>(uid))
            return;

        // Don't add if already has the component
        if (HasComp<SharedBatteryItemComponent>(uid))
            return;

        // Check if the entity has a battery or power cell slot
        if (!HasComp<BatteryComponent>(uid) && !HasComp<PowerCellSlotComponent>(uid))
            return;

        EnsureComp<SharedBatteryItemComponent>(uid);
    }
}