using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Whitelist;
using Content.Shared.Vehicle;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Generic base system to handle installing items (equipment/modules) into mechs.
/// Reduces duplication between MechEquipmentSystem and MechModuleSystem.
/// </summary>
public abstract class MechInstallBaseSystem<TItemComponent, TInsertEvent> : EntitySystem
    where TItemComponent : Component
    where TInsertEvent : SimpleDoAfterEvent, new()
{
    [Dependency] protected readonly SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected readonly PopupSystem Popup = default!;
    [Dependency] protected readonly EntityWhitelistSystem Whitelist = default!;
    [Dependency] protected readonly VehicleSystem Vehicle = default!;
    [Dependency] protected readonly SharedMechSystem MechSystem = default!;

    // Unified, shared locale keys across equipment and modules

    public override void Initialize()
    {
        SubscribeLocalEvent<TItemComponent, AfterInteractEvent>(OnUsed);
        SubscribeLocalEvent<TItemComponent, TInsertEvent>(OnInsert);
    }

    protected virtual IReadOnlyList<EntityUid> GetInstalled(MechComponent mech)
    {
        return Array.Empty<EntityUid>();
    }

    protected virtual int GetInstalledCount(MechComponent mech) => 0;
    protected virtual int GetMaxInstall(MechComponent mech) => 0;
    protected abstract bool IsWhitelistFail(MechComponent mech, EntityUid used);
    protected abstract void PerformInsert(EntityUid mech, EntityUid item, MechComponent mechComp, TItemComponent itemComp);

    private void OnUsed(EntityUid uid, TItemComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        var mech = args.Target.Value;
        if (!TryComp<MechComponent>(mech, out var mechComp))
            return;

        // Block install if mech is in broken state
        if (mechComp.Broken && !Vehicle.HasOperator(mech))
        {
            Popup.PopupEntity(Loc.GetString("mech-cannot-insert-broken"), args.User);
            return;
        }

        // Block install if cabin is closed
        if (Vehicle.HasOperator(mech))
        {
            Popup.PopupEntity(Loc.GetString("mech-cannot-modify-closed"), args.User);
            return;
        }

        if (args.User == Vehicle.GetOperatorOrNull(mech))
            return;

        // Duplicate by prototype id
        var md = EntityManager.GetComponentOrNull<MetaDataComponent>(uid);
        if (md?.EntityPrototype != null)
        {
            var id = md.EntityPrototype.ID;
            foreach (var ent in GetInstalled(mechComp))
            {
                var md2 = EntityManager.GetComponentOrNull<MetaDataComponent>(ent);
                if (md2?.EntityPrototype != null && md2.EntityPrototype.ID == id)
                {
                    Popup.PopupEntity(Loc.GetString("mech-duplicate-installed-popup"), args.User);
                    return;
                }
            }
        }

        if (GetInstalledCount(mechComp) >= GetMaxInstall(mechComp))
        {
            Popup.PopupEntity(Loc.GetString("mech-capacity-full-popup"), args.User);
            return;
        }

        if (IsWhitelistFail(mechComp, args.Used))
            return;

        Popup.PopupEntity(Loc.GetString("mech-install-begin-popup", ("item", uid)), mech);

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, GetInstallDuration(uid, component), new TInsertEvent(), uid, target: mech, used: uid)
        {
            BreakOnMove = true,
        };

        DoAfter.TryStartDoAfter(doAfterEventArgs);
    }

    protected abstract float GetInstallDuration(EntityUid uid, TItemComponent comp);

    private void OnInsert(EntityUid uid, TItemComponent component, TInsertEvent args)
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

        Popup.PopupEntity(Loc.GetString("mech-install-finish-popup", ("item", uid)), mech);
        PerformInsert(mech, uid, mechComp, component);

        args.Handled = true;
    }
}

