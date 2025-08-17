using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Vehicle;
using Content.Shared.Whitelist;
using Robust.Server.Containers;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Handles the insertion of mech equipment into mechs.
/// </summary>
public sealed class MechEquipmentSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly VehicleSystem _vehicle = default!;
    [Dependency] private readonly SharedMechSystem _mechSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MechEquipmentComponent, AfterInteractEvent>(OnUsed);
        SubscribeLocalEvent<MechEquipmentComponent, InsertEquipmentEvent>(OnInsertEquipment);
    }

    private void OnUsed(EntityUid uid, MechEquipmentComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        var mech = args.Target.Value;
        if (!TryComp<MechComponent>(mech, out var mechComp))
            return;

        // Block install if mech is in broken state
        if (mechComp.Broken && !_vehicle.HasOperator(mech))
        {
            _popup.PopupEntity(Loc.GetString("mech-cannot-insert-broken"), args.User);
            return;
        }

        // Block install if cabin is closed
        if (_vehicle.HasOperator(mech))
        {
            _popup.PopupEntity(Loc.GetString("mech-cannot-modify-closed"), args.User);
            return;
        }

        if (args.User == _vehicle.GetOperatorOrNull(mech))
            return;

        // Duplicate by prototype id
        var md = EntityManager.GetComponentOrNull<MetaDataComponent>(uid);
        if (md != null && md.EntityPrototype != null)
        {
            var id = md.EntityPrototype.ID;
            foreach (var ent in mechComp.EquipmentContainer.ContainedEntities)
            {
                var md2 = EntityManager.GetComponentOrNull<MetaDataComponent>(ent);
                if (md2 != null && md2.EntityPrototype != null && md2.EntityPrototype.ID == id)
                {
                    _popup.PopupEntity(Loc.GetString("mech-duplicate-equipment-popup"), args.User);
                    return;
                }
            }
        }

        if (mechComp.EquipmentContainer.ContainedEntities.Count >= mechComp.MaxEquipmentAmount)
        {
            _popup.PopupEntity(Loc.GetString("mech-capacity-equipment-full-popup"), args.User);
            return;
        }

        if (_whitelistSystem.IsWhitelistFail(mechComp.EquipmentWhitelist, args.Used))
            return;

        _popup.PopupEntity(Loc.GetString("mech-equipment-begin-install-popup", ("item", uid)), mech);

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.InstallDuration, new InsertEquipmentEvent(), uid, target: mech, used: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnInsertEquipment(EntityUid uid, MechEquipmentComponent component, InsertEquipmentEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        // Access check
        var mech = args.Args.Target.Value;
        if (TryComp<MechLockComponent>(mech, out var lockComp) && lockComp.IsLocked)
        {
            var lockSys = EntityManager.System<MechLockSystem>();
            if (!lockSys.CheckAccessWithFeedback(mech, args.Args.User, lockComp))
                return;
        }

        if (!TryComp<MechComponent>(mech, out var mechComp))
            return;

        _popup.PopupEntity(Loc.GetString("mech-equipment-finish-install-popup", ("item", uid)), mech);
        _mechSystem.InsertEquipment(mech, uid, mechComp, component);

        args.Handled = true;
    }
}
