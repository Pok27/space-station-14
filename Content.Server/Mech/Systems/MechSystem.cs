using Content.Server.Mech.Components;
using Content.Server.Mech.Events;
using Content.Server.Mech.Equipment.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mech;
using Content.Shared.Popups;
using System.Linq;
using Content.Shared.Movement.Events;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Body.Events;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Mech.Systems;

/// <inheritdoc/>
public sealed partial class MechSystem : SharedMechSystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly MechLockSystem _lockSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<MechComponent, RepairMechEvent>(OnRepairMechEvent);
        SubscribeLocalEvent<MechComponent, EntInsertedIntoContainerMessage>(OnInsertBattery);
        SubscribeLocalEvent<MechComponent, RemoveBatteryEvent>(OnRemoveBattery);
        SubscribeLocalEvent<MechComponent, MechEntryEvent>(OnMechEntry);
        SubscribeLocalEvent<MechComponent, MechExitEvent>(OnMechExit);
        SubscribeLocalEvent<MechComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MechComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MechComponent, BeingGibbedEvent>(OnBeingGibbed);
        SubscribeLocalEvent<MechComponent, UpdateCanMoveEvent>(OnMechCanMoveEvent);

        SubscribeLocalEvent<MechPilotComponent, ToolUserAttemptUseEvent>(OnToolUseAttempt);
        SubscribeLocalEvent<MechComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
        SubscribeAllEvent<RequestMechEquipmentSelectEvent>(OnEquipmentSelectRequest);
        SubscribeLocalEvent<MechComponent, MechOpenUiEvent>(OnOpenUi);
        SubscribeLocalEvent<MechComponent, MechBrokenSoundEvent>(OnMechBrokenSound);
    }

    private void OnRepairMechEvent(EntityUid uid, MechComponent component, RepairMechEvent args)
    {
        RepairMech(uid, component);
    }

    private void OnMechCanMoveEvent(EntityUid uid, MechComponent component, UpdateCanMoveEvent args)
    {
        // Block movement if mech is in broken state or has no energy/integrity
        if (component.Broken || component.Integrity <= 0 || component.Energy <= 0)
        {
            args.Cancel();
            return;
        }

        // Block movement if mech is locked and pilot lacks access
        if (TryComp<MechLockComponent>(uid, out var lockComp) && lockComp.IsLocked)
        {
            var pilot = Vehicle.GetOperatorOrNull(uid);
            if (pilot.HasValue && !_lockSystem.CheckAccess(uid, pilot.Value, lockComp))
            {
                args.Cancel();
                return;
            }
        }
    }

    private void OnInteractUsing(EntityUid uid, MechComponent component, InteractUsingEvent args)
    {
        if (TryComp<WiresPanelComponent>(uid, out var panel) && !panel.Open)
            return;

        if (component.BatterySlot.ContainedEntity == null && TryComp<BatteryComponent>(args.Used, out var battery))
        {
            if (Vehicle.HasOperator(uid))
            {
                _popup.PopupEntity(Loc.GetString("mech-cannot-modify-closed"), args.User);
                return;
            }

            InsertBattery(uid, args.Used, component, battery);
            _actionBlocker.UpdateCanMove(uid);
            return;
        }

        if (_toolSystem.HasQuality(args.Used, PryingQuality) && component.BatterySlot.ContainedEntity != null)
        {
            if (Vehicle.HasOperator(uid))
            {
                _popup.PopupEntity(Loc.GetString("mech-cannot-modify-closed"), args.User);
                return;
            }

            var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.BatteryRemovalDelay,
                new RemoveBatteryEvent(), uid, target: uid, used: args.Target)
            {
                BreakOnMove = true
            };

            _doAfter.TryStartDoAfter(doAfterEventArgs);
            return;
        }
    }

    private void OnInsertBattery(EntityUid uid, MechComponent component, EntInsertedIntoContainerMessage args)
    {
        if (args.Container != component.BatterySlot || !TryComp<BatteryComponent>(args.Entity, out var battery))
            return;

        component.Energy = battery.CurrentCharge;
        component.MaxEnergy = battery.MaxCharge;

        Dirty(uid, component);
        _actionBlocker.UpdateCanMove(uid);
    }

    private void OnRemoveBattery(EntityUid uid, MechComponent component, RemoveBatteryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        RemoveBattery(uid, component);
        _actionBlocker.UpdateCanMove(uid);

        args.Handled = true;
    }

    private void OnMapInit(EntityUid uid, MechComponent component, MapInitEvent args)
    {
        var xform = Transform(uid);

        foreach (var equipment in component.StartingEquipment)
        {
            var ent = Spawn(equipment, xform.Coordinates);
            InsertEquipment(uid, ent, component);
        }

        foreach (var module in component.StartingModules)
        {
            var ent = Spawn(module, xform.Coordinates);
            InsertEquipment(uid, ent, component);
        }

        // Ensure cabin pressure component is present for airtight operation
        EnsureComp<MechCabinAirComponent>(uid);

        component.Integrity = component.MaxIntegrity;
        component.Energy = component.MaxEnergy;
        component.Airtight = false;

        SetIntegrity(uid, component.MaxIntegrity, component);
        _actionBlocker.UpdateCanMove(uid);
    }

    private void OnOpenUi(EntityUid uid, MechComponent component, MechOpenUiEvent args)
    {
        // For InstantActionEvent, we need to get the user from the event context
        var user = args.Performer;

        // UI can always be opened, access control is handled in the UI itself
        args.Handled = true;
        ToggleMechUi(uid, component);
    }

    private void OnToolUseAttempt(EntityUid uid, MechPilotComponent component, ref ToolUserAttemptUseEvent args)
    {
        if (args.Target == component.Mech)
            args.Cancelled = true;
    }

    private void OnAlternativeVerb(EntityUid uid, MechComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Always add UI open verb
        var openUiVerb = new AlternativeVerb
        {
            Act = () => ToggleMechUi(uid, component, args.User),
            Text = Loc.GetString("mech-ui-open-verb")
        };
        args.Verbs.Add(openUiVerb);

        if (CanInsert(uid, args.User, component))
        {
            var enterVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mech-verb-enter"),
                Act = () =>
                {
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.EntryDelay, new MechEntryEvent(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };

                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            args.Verbs.Add(enterVerb);
        }
        else if (Vehicle.HasOperator(uid))
        {
            var ejectVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mech-verb-exit"),
                Priority = 1, // Promote to top to make ejecting the ALT-click action
                Act = () =>
                {
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.ExitDelay, new MechExitEvent(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };
                    if (args.User != uid && args.User != component.PilotSlot.ContainedEntity)
                        _popup.PopupEntity(Loc.GetString("mech-eject-pilot-alert-popup", ("item", uid), ("user", args.User)), uid, PopupType.Large);

                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            args.Verbs.Add(ejectVerb);
        }
    }

    private void OnMechEntry(EntityUid uid, MechComponent component, MechEntryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        // Allow entry if locks are not active; block only if active and user lacks access
        if (TryComp<MechLockComponent>(uid, out var lockComp) && lockComp.IsLocked && !_lockSystem.CheckAccess(uid, args.User, lockComp))
        {
            _lockSystem.CheckAccessWithFeedback(uid, args.User, lockComp);
            return;
        }

        if (!Vehicle.CanOperate(uid, args.User))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-enter-popup", ("item", uid)), args.User);
            return;
        }

        TryInsert(uid, args.Args.User, component);
        args.Handled = true;
    }

    private void OnMechExit(EntityUid uid, MechComponent component, MechExitEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        TryEject(uid, component);

        args.Handled = true;
    }

    private void OnDamageChanged(EntityUid uid, MechComponent component, DamageChangedEvent args)
    {
        var integrity = component.MaxIntegrity - args.Damageable.TotalDamage;
        SetIntegrity(uid, integrity, component);
    }

    private void ToggleMechUi(EntityUid uid, MechComponent? component = null, EntityUid? user = null)
    {
        if (!Resolve(uid, ref component))
            return;
        user ??= Vehicle.GetOperatorOrNull(uid);
        if (user == null)
            return;

        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        // Open UI using UserInterfaceSystem
        var ui = EntityManager.System<UserInterfaceSystem>();
        ui.TryToggleUi(uid, MechUiKey.Key, actor.PlayerSession);
    }

    public bool TryGetGasModuleAir(EntityUid mechUid, out GasMixture? air)
    {
        air = null;
        if (!TryComp<MechComponent>(mechUid, out var mech))
            return false;

        foreach (var ent in mech.ModuleContainer.ContainedEntities)
        {
            if (TryComp<MechAirTankModuleComponent>(ent, out _))
            {
                if (TryComp<GasTankComponent>(ent, out var tank))
                {
                    air = tank.Air;
                    return true;
                }
                return false;
            }
        }

        return false;
    }

    public override void BreakMech(EntityUid uid, MechComponent? component = null)
    {
        base.BreakMech(uid, component);
        _actionBlocker.UpdateCanMove(uid);
    }

    public override bool TryChangeEnergy(EntityUid uid, FixedPoint2 delta, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        var newEnergy = component.Energy + delta;
        if (newEnergy < 0 || newEnergy > component.MaxEnergy)
            return false;

        component.Energy = newEnergy;
        Dirty(uid, component);
        UpdateMechUi(uid);
        return true;
    }

    public void InsertBattery(EntityUid uid, EntityUid toInsert, MechComponent? component = null, BatteryComponent? battery = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (!Resolve(toInsert, ref battery, false))
            return;

        _container.Insert(toInsert, component.BatterySlot);
        component.Energy = battery.CurrentCharge;
        component.MaxEnergy = battery.MaxCharge;

        _actionBlocker.UpdateCanMove(uid);
        Dirty(uid, component);
        UpdateMechUi(uid);
    }

    public void RemoveBattery(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        _container.EmptyContainer(component.BatterySlot);
        component.Energy = 0;
        component.MaxEnergy = 0;

        _actionBlocker.UpdateCanMove(uid);
        Dirty(uid, component);
        UpdateMechUi(uid);
    }

    private void UpdateMechUi(EntityUid uid)
    {
        var ev = new UpdateMechUiEvent();
        RaiseLocalEvent(uid, ev);
    }

    private void OnMechBrokenSound(EntityUid uid, MechComponent component, MechBrokenSoundEvent args)
    {
        _audio.PlayPvs(args.Sound, uid);
    }

    public override bool CanInsert(EntityUid uid, EntityUid toInsert, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        return base.CanInsert(uid, toInsert, component) && _actionBlocker.CanMove(toInsert);
    }

    private void OnEquipmentSelectRequest(RequestMechEquipmentSelectEvent args, EntitySessionEventArgs session)
    {
        var user = session.SenderSession.AttachedEntity;
        if (user == null)
            return;
        if (!TryComp<MechPilotComponent>(user.Value, out var pilot))
            return;
        var mech = pilot.Mech;
        if (!TryComp<MechComponent>(mech, out var mechComp))
            return;

        if (args.Equipment == null)
        {
            mechComp.CurrentSelectedEquipment = null;
            _popup.PopupEntity(Loc.GetString("mech-equipment-select-none-popup"), mech);
        }
        else
        {
            var equipment = GetEntity(args.Equipment);
            if (Exists(equipment) && mechComp.EquipmentContainer.ContainedEntities.Any(e => e == equipment))
            {
                mechComp.CurrentSelectedEquipment = equipment;
                _popup.PopupEntity(Loc.GetString("mech-equipment-select-popup", ("item", equipment)), mech);
            }
        }

        Dirty(mech, mechComp);
        RefreshPilotHandVirtualItems(mech, mechComp);
    }

    private void OnBeingGibbed(EntityUid uid, MechComponent component, ref BeingGibbedEvent args)
    {
        // Eject pilot if present
        if (component.PilotSlot.ContainedEntity != null)
        {
            TryEject(uid, component);
        }

        if (component.PilotSlot.ContainedEntity != null)
            args.GibbedParts.Add(component.PilotSlot.ContainedEntity.Value);

        // TODO: Parts should fall out
        QueueDel(uid);
    }
}
