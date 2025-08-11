using Content.Server.Mech.Systems;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Server.Containers;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Handles the insertion of mech passive modules into mechs analogous to equipment, but into a different container.
/// </summary>
public sealed class MechModuleSystem : EntitySystem
{
    [Dependency] private readonly MechSystem _mech = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly ContainerSystem _container = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MechModuleComponent, AfterInteractEvent>(OnUsed);
        SubscribeLocalEvent<MechModuleComponent, InsertModuleEvent>(OnInsertModule);
    }

    private void OnUsed(EntityUid uid, MechModuleComponent module, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        var mech = args.Target.Value;
        if (!TryComp<MechComponent>(mech, out var mechComp))
            return;

        if (mechComp.Broken)
            return;

        // Block install if pilot inside
        if (mechComp.PilotSlot.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("mech-install-blocked-pilot-popup", ("item", uid)), args.User);
            return;
        }

        // Cannot install from inside while piloting, same as equipment
        if (args.User == mechComp.PilotSlot.ContainedEntity)
            return;

        if (_whitelistSystem.IsWhitelistFail(mechComp.ModuleWhitelist, args.Used))
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

        // Duplicate by component type (e.g., only 1 fan, only 1 gas cylinder)
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

        // Capacity check (sum sizes in module container)
        var used = 0;
        foreach (var ent in mechComp.ModuleContainer.ContainedEntities)
        {
            if (TryComp<MechModuleComponent>(ent, out var m))
                used += m.Size;
        }
        if (used + module.Size > mechComp.MaxModuleSpace)
        {
            _popup.PopupEntity(Loc.GetString("mech-capacity-modules-full-popup"), args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("mech-equipment-begin-install-popup", ("item", uid)), mech);

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, module.InstallDuration, new InsertModuleEvent(), uid, target: mech, used: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnInsertModule(EntityUid uid, MechModuleComponent module, InsertModuleEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        var mech = args.Args.Target.Value;
        if (!TryComp<MechComponent>(mech, out var mechComp))
            return;

        if (TryComp<MechLockComponent>(mech, out var lockComp) && lockComp.IsLocked)
        {
            var lockSys = EntityManager.System<MechLockSystem>();
            if (!lockSys.CheckAccessWithFeedback(mech, args.Args.User, lockComp))
                return;
        }

        // Insert into module container
        _container.Insert(uid, mechComp.ModuleContainer);

        _popup.PopupEntity(Loc.GetString("mech-equipment-finish-install-popup", ("item", uid)), mech);
        RaiseLocalEvent(mech, new UpdateMechUiEvent());

        args.Handled = true;
    }
}
