using Content.Shared.Power.Components;
using Content.Server.Power.Components;
using Content.Shared.PowerCell.Components;
using Content.Shared.Item;
using Robust.Shared.Containers;

namespace Content.Server.Power.EntitySystems;

/// <summary>
/// Automatically adds BatteryItemStatusComponent to items that have batteries,
/// making battery status visible when examining items.
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
        if (HasComp<BatteryItemStatusComponent>(uid))
            return;

        // Check if the entity has a battery or power cell slot
        if (!HasComp<BatteryComponent>(uid) && !HasComp<PowerCellSlotComponent>(uid))
            return;

        EnsureComp<BatteryItemStatusComponent>(uid);
    }
}