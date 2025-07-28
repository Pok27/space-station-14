using Content.Server.Power.Components;
using Content.Server.PowerCell;
using Content.Shared.Power;
using Content.Shared.PowerCell.Components;

namespace Content.Server.Power.EntitySystems;

/// <summary>
/// Handles battery information requests for UI components.
/// </summary>
public sealed class BatteryInfoSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _powerCell = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BatteryComponent, GetBatteryInfoEvent>(OnGetBatteryInfo);
        SubscribeLocalEvent<PowerCellSlotComponent, GetBatteryInfoEvent>(OnGetPowerCellInfo);
        
        // Subscribe to battery changes to update shared state
        SubscribeLocalEvent<BatteryComponent, ChargeChangedEvent>(OnChargeChanged);
        
        // Subscribe to component startup to ensure SharedBatteryState is added
        SubscribeLocalEvent<BatteryComponent, ComponentStartup>(OnBatteryStartup);
        SubscribeLocalEvent<PowerCellSlotComponent, ComponentStartup>(OnPowerCellStartup);
        
        // Subscribe to power cell changes
        SubscribeLocalEvent<PowerCellSlotComponent, PowerCellChangedEvent>(OnPowerCellChanged);
    }

    private void OnGetBatteryInfo(Entity<BatteryComponent> entity, ref GetBatteryInfoEvent args)
    {
        var battery = entity.Comp;
        if (battery.MaxCharge > 0)
        {
            args.ChargePercent = battery.CurrentCharge / battery.MaxCharge;
            args.HasBattery = true;
        }
        
        // Also update the shared state component
        UpdateSharedBatteryState(entity.Owner, args.ChargePercent, args.HasBattery);
    }

    private void OnGetPowerCellInfo(Entity<PowerCellSlotComponent> entity, ref GetBatteryInfoEvent args)
    {
        if (_powerCell.TryGetBatteryFromSlot(entity.Owner, out var battery, entity.Comp))
        {
            if (battery.MaxCharge > 0)
            {
                args.ChargePercent = battery.CurrentCharge / battery.MaxCharge;
                args.HasBattery = true;
            }
        }
        
        // Also update the shared state component
        UpdateSharedBatteryState(entity.Owner, args.ChargePercent, args.HasBattery);
    }

    private void OnChargeChanged(Entity<BatteryComponent> entity, ref ChargeChangedEvent args)
    {
        // Update shared battery state when charge changes
        var chargePercent = args.MaxCharge > 0 ? args.Charge / args.MaxCharge : 0f;
        UpdateSharedBatteryState(entity.Owner, chargePercent, args.MaxCharge > 0);
    }

    private void OnBatteryStartup(Entity<BatteryComponent> entity, ref ComponentStartup args)
    {
        // Ensure shared battery state is available and initialized
        var chargePercent = entity.Comp.MaxCharge > 0 ? entity.Comp.CurrentCharge / entity.Comp.MaxCharge : 0f;
        UpdateSharedBatteryState(entity.Owner, chargePercent, entity.Comp.MaxCharge > 0);
    }

    private void OnPowerCellStartup(Entity<PowerCellSlotComponent> entity, ref ComponentStartup args)
    {
        // Check if there's a power cell and initialize shared state
        if (_powerCell.TryGetBatteryFromSlot(entity.Owner, out var battery, entity.Comp))
        {
            var chargePercent = battery.MaxCharge > 0 ? battery.CurrentCharge / battery.MaxCharge : 0f;
            UpdateSharedBatteryState(entity.Owner, chargePercent, battery.MaxCharge > 0);
        }
        else
        {
            UpdateSharedBatteryState(entity.Owner, 0f, false);
        }
    }

    private void OnPowerCellChanged(Entity<PowerCellSlotComponent> entity, ref PowerCellChangedEvent args)
    {
        // Update shared battery state when a power cell is changed
        if (_powerCell.TryGetBatteryFromSlot(entity.Owner, out var battery, entity.Comp))
        {
            var chargePercent = battery.MaxCharge > 0 ? battery.CurrentCharge / battery.MaxCharge : 0f;
            UpdateSharedBatteryState(entity.Owner, chargePercent, battery.MaxCharge > 0);
        }
        else
        {
            UpdateSharedBatteryState(entity.Owner, 0f, false);
        }
    }

    private void UpdateSharedBatteryState(EntityUid uid, float chargePercent, bool hasBattery)
    {
        // Ensure the entity has a shared battery state component
        if (!TryComp<SharedBatteryStateComponent>(uid, out var sharedState))
        {
            sharedState = AddComp<SharedBatteryStateComponent>(uid);
        }

        // Update the networked values
        sharedState.ChargePercent = chargePercent;
        sharedState.HasBattery = hasBattery;
        Dirty(uid, sharedState);
    }
}
