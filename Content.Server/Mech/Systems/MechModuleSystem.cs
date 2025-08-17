using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Server.Containers;
using Content.Shared.Vehicle;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Handles the insertion of mech module into mechs.
/// </summary>
public sealed class MechModuleSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly VehicleSystem _vehicle = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MechModuleComponent, AfterInteractEvent>(OnUsed);
        SubscribeLocalEvent<MechModuleComponent, InsertModuleEvent>(OnInsertModule);
    }

    private void OnUsed(EntityUid uid, MechModuleComponent component, AfterInteractEvent args)
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

        if (args.User == mechComp.PilotSlot.ContainedEntity)
            return;

        // Duplicate by prototype id
        var md = EntityManager.GetComponentOrNull<MetaDataComponent>(uid);
        if (md != null && md.EntityPrototype != null)
        {
            var id = md.EntityPrototype.ID;
            foreach (var ent in mechComp.ModuleContainer.ContainedEntities)
            {
                var md2 = EntityManager.GetComponentOrNull<MetaDataComponent>(ent);
                if (md2 != null && md2.EntityPrototype != null && md2.EntityPrototype.ID == id)
                {
                    _popup.PopupEntity(Loc.GetString("mech-duplicate-module-popup"), args.User);
                    return;
                }
            }
        }

        // Duplicate by component type (e.g., only 1 fan, only 1 gas cylinder).
        var hasFan = false;
        var hasGas = false;
        foreach (var ent in mechComp.ModuleContainer.ContainedEntities)
        {
            hasFan |= HasComp<MechFanModuleComponent>(ent);
            hasGas |= HasComp<MechGasCylinderModuleComponent>(ent);
        }
        if ((hasFan && HasComp<MechFanModuleComponent>(uid)) || (hasGas && HasComp<MechGasCylinderModuleComponent>(uid)))
        {
            _popup.PopupEntity(Loc.GetString("mech-duplicate-module-popup"), args.User);
            return;
        }

        if (mechComp.ModuleContainer.ContainedEntities.Count > mechComp.MaxModuleAmount)
        {
            _popup.PopupEntity(Loc.GetString("mech-capacity-modules-full-popup"), args.User);
            return;
        }

        if (_whitelistSystem.IsWhitelistFail(mechComp.ModuleWhitelist, args.Used))
            return;

        _popup.PopupEntity(Loc.GetString("mech-equipment-begin-install-popup", ("item", uid)), mech);

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.InstallDuration, new InsertModuleEvent(), uid, target: mech, used: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnInsertModule(EntityUid uid, MechModuleComponent component, InsertModuleEvent args)
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
        EntityManager.System<MechSystem>().InsertEquipment(mech, uid, mechComp);

        args.Handled = true;
    }
}
