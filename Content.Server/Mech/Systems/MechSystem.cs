using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Server.Mech.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Content.Shared.Wires;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Server.Access.Systems;
using Content.Server.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Mech.Components;
using Content.Shared.Atmos;

namespace Content.Server.Mech.Systems;

/// <inheritdoc/>
public sealed partial class MechSystem : SharedMechSystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly MechLockSystem _lockSystem = default!;

    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<MechComponent, EntInsertedIntoContainerMessage>(OnInsertBattery);
        SubscribeLocalEvent<MechComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MechComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
        SubscribeLocalEvent<MechComponent, MechOpenUiEvent>(OnOpenUi);
        SubscribeLocalEvent<MechComponent, RemoveBatteryEvent>(OnRemoveBattery);
        SubscribeLocalEvent<MechComponent, MechEntryEvent>(OnMechEntry);
        SubscribeLocalEvent<MechComponent, MechExitEvent>(OnMechExit);
        SubscribeLocalEvent<MechComponent, MechAirtightMessage>(OnAirtightMessage);
        SubscribeLocalEvent<MechComponent, MechFanToggleMessage>(OnFanToggleMessage);
        SubscribeLocalEvent<MechComponent, MechEquipmentSelectMessage>(OnEquipmentSelectMessage);

        SubscribeLocalEvent<MechComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MechComponent, MechEquipmentRemoveMessage>(OnRemoveEquipmentMessage);

        SubscribeLocalEvent<MechComponent, UpdateCanMoveEvent>(OnMechCanMoveEvent);

        SubscribeLocalEvent<MechPilotComponent, ToolUserAttemptUseEvent>(OnToolUseAttempt);
        SubscribeLocalEvent<MechPilotComponent, InhaleLocationEvent>(OnInhale);
        SubscribeLocalEvent<MechPilotComponent, ExhaleLocationEvent>(OnExhale);
        SubscribeLocalEvent<MechPilotComponent, AtmosExposedGetAirEvent>(OnExpose);

        SubscribeLocalEvent<MechAirComponent, GetFilterAirEvent>(OnGetFilterAir);

        #region Equipment UI message relays
        SubscribeLocalEvent<MechComponent, MechGrabberEjectMessage>(ReceiveEquipmentUiMesssages);
        SubscribeLocalEvent<MechComponent, MechSoundboardPlayMessage>(ReceiveEquipmentUiMesssages);
        #endregion

        #region Lock system
        SubscribeLocalEvent<MechComponent, BoundUserInterfaceMessageAttempt>(OnBoundUIAttempt);
        SubscribeLocalEvent<MechComponent, UpdateMechUiEvent>(OnUpdateMechUi);
        #endregion
    }

    private void OnMechCanMoveEvent(EntityUid uid, MechComponent component, UpdateCanMoveEvent args)
    {
        if (component.Broken || component.Integrity <= 0 || component.Energy <= 0)
            args.Cancel();

        // Check if mech is locked and pilot doesn't have access
        if (TryComp<MechLockComponent>(uid, out var lockComp) &&
            lockComp.IsLocked &&
            component.PilotSlot.ContainedEntity != null)
        {
            if (!_lockSystem.HasAccess(component.PilotSlot.ContainedEntity.Value, lockComp))
            {
                args.Cancel();
            }
        }
    }

    private void OnInteractUsing(EntityUid uid, MechComponent component, InteractUsingEvent args)
    {
        // Check if mech is locked and user doesn't have access
        if (TryComp<MechLockComponent>(uid, out var lockComp) &&
            lockComp.IsLocked &&
            !_lockSystem.HasAccess(args.User, lockComp))
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-access-denied"), uid, args.User);
            args.Handled = true;
            return;
        }

        if (TryComp<WiresPanelComponent>(uid, out var panel) && !panel.Open)
            return;

        if (component.BatterySlot.ContainedEntity == null && TryComp<BatteryComponent>(args.Used, out var battery))
        {
            InsertBattery(uid, args.Used, component, battery);
            _actionBlocker.UpdateCanMove(uid);
            return;
        }

        if (_toolSystem.HasQuality(args.Used, PryingQuality) && component.BatterySlot.ContainedEntity != null)
        {
            var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.BatteryRemovalDelay,
                new RemoveBatteryEvent(), uid, target: uid, used: args.Target)
            {
                BreakOnMove = true
            };

            _doAfter.TryStartDoAfter(doAfterEventArgs);
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
        // TODO: this should use containerfill?
        foreach (var equipment in component.StartingEquipment)
        {
            var ent = Spawn(equipment, xform.Coordinates);
            InsertEquipment(uid, ent, component);
        }

        // TODO: this should just be damage and battery
        component.Integrity = component.MaxIntegrity;
        component.Energy = component.MaxEnergy;

        _actionBlocker.UpdateCanMove(uid);
        Dirty(uid, component);
    }

    private void OnRemoveEquipmentMessage(EntityUid uid, MechComponent component, MechEquipmentRemoveMessage args)
    {
        var equip = GetEntity(args.Equipment);

        if (!Exists(equip) || Deleted(equip))
            return;

        if (!component.EquipmentContainer.ContainedEntities.Contains(equip))
            return;

        RemoveEquipment(uid, equip, component);
    }

    private void OnOpenUi(EntityUid uid, MechComponent component, MechOpenUiEvent args)
    {
        // For InstantActionEvent, we need to get the user from the event context
        var user = args.Performer;

        // Check if mech is locked and user doesn't have access
        if (TryComp<MechLockComponent>(uid, out var lockComp) &&
            lockComp.IsLocked &&
            !_lockSystem.HasAccess(user, lockComp))
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-access-denied"), uid, user);
            return;
        }

        args.Handled = true;
        ToggleMechUi(uid, component);
    }

    private void OnAirtightMessage(EntityUid uid, MechComponent component, MechAirtightMessage args)
    {
        component.Airtight = args.IsAirtight;
        Dirty(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void OnFanToggleMessage(EntityUid uid, MechComponent component, MechFanToggleMessage args)
    {
        if (!TryComp<MechFanComponent>(uid, out var fanComp))
            return;

        fanComp.IsActive = args.IsActive;
        Dirty(uid, fanComp);
    }

    private void OnEquipmentSelectMessage(EntityUid uid, MechComponent component, MechEquipmentSelectMessage args)
    {
        if (args.Equipment == null)
        {
            component.CurrentSelectedEquipment = null;
        }
        else
        {
            var equipment = GetEntity(args.Equipment);
            if (Exists(equipment) && component.EquipmentContainer.ContainedEntities.Any(e => e == equipment))
            {
                component.CurrentSelectedEquipment = equipment;
            }
        }

        Dirty(uid, component);
    }

    private void OnToolUseAttempt(EntityUid uid, MechPilotComponent component, ref ToolUserAttemptUseEvent args)
    {
        if (args.Target == component.Mech)
            args.Cancelled = true;

        // Check if mech is locked and pilot doesn't have access
        if (TryComp<MechLockComponent>(component.Mech, out var lockComp) &&
            lockComp.IsLocked && !_lockSystem.HasAccess(uid, lockComp))
        {
            args.Cancelled = true;
        }
    }

    private void OnAlternativeVerb(EntityUid uid, MechComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || component.Broken)
            return;

        // Check if mech is locked and user doesn't have access
        if (TryComp<MechLockComponent>(uid, out var lockComp) &&
            lockComp.IsLocked &&
            !_lockSystem.HasAccess(args.User, lockComp))
            return;

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
            var openUiVerb = new AlternativeVerb //can't hijack someone else's mech
            {
                Act = () => ToggleMechUi(uid, component, args.User),
                Text = Loc.GetString("mech-ui-open-verb")
            };
            args.Verbs.Add(enterVerb);
            args.Verbs.Add(openUiVerb);
        }
        else if (!IsEmpty(component))
        {
            var ejectVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mech-verb-exit"),
                Priority = 1, // Promote to top to make ejecting the ALT-click action
                Act = () =>
                {
                    if (args.User == uid || args.User == component.PilotSlot.ContainedEntity)
                    {
                        TryEject(uid, component);
                        return;
                    }

                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.ExitDelay, new MechExitEvent(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };
                    _popup.PopupEntity(Loc.GetString("mech-eject-pilot-alert", ("item", uid), ("user", args.User)), uid, PopupType.Large);

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

        // Check if mech is locked and user doesn't have access
        if (TryComp<MechLockComponent>(uid, out var lockComp) &&
            lockComp.IsLocked &&
            !_lockSystem.HasAccess(args.User, lockComp))
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-access-denied"), uid, args.User);
            return;
        }

        if (_whitelistSystem.IsWhitelistFail(component.PilotWhitelist, args.User))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-enter", ("item", uid)), args.User);
            return;
        }

        TryInsert(uid, args.Args.User, component);
        _actionBlocker.UpdateCanMove(uid);

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

        if (args.DamageIncreased &&
            args.DamageDelta != null &&
            component.PilotSlot.ContainedEntity != null)
        {
            var damage = args.DamageDelta * component.MechToPilotDamageMultiplier;
            _damageable.TryChangeDamage(component.PilotSlot.ContainedEntity, damage);
        }
    }

    private void ToggleMechUi(EntityUid uid, MechComponent? component = null, EntityUid? user = null)
    {
        if (!Resolve(uid, ref component))
            return;
        user ??= component.PilotSlot.ContainedEntity;
        if (user == null)
            return;

        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        _ui.TryToggleUi(uid, MechUiKey.Key, actor.PlayerSession);
        UpdateUserInterface(uid, component);
    }

    private void ReceiveEquipmentUiMesssages<T>(EntityUid uid, MechComponent component, T args) where T : MechEquipmentUiMessage
    {
        var ev = new MechEquipmentUiMessageRelayEvent(args);
        var allEquipment = new List<EntityUid>(component.EquipmentContainer.ContainedEntities);
        var argEquip = GetEntity(args.Equipment);

        foreach (var equipment in allEquipment)
        {
            if (argEquip == equipment)
                RaiseLocalEvent(equipment, ev);
        }
    }

    private void OnBoundUIAttempt(Entity<MechComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.UiKey is not MechUiKey.Key)
            return;

        var actor = args.Actor;
        var message = args.Message;

        // Forward lock-related messages to MechLockComponent if it exists
        if (HasComp<MechLockComponent>(ent.Owner))
        {
            switch (message)
            {
                case MechDnaLockRegisterMessage:
                    RaiseLocalEvent(ent.Owner, new MechDnaLockRegisterEvent { User = GetNetEntity(actor) });
                    break;
                case MechDnaLockToggleMessage:
                    RaiseLocalEvent(ent.Owner, new MechDnaLockToggleEvent { User = GetNetEntity(actor) });
                    break;
                case MechDnaLockResetMessage:
                    RaiseLocalEvent(ent.Owner, new MechDnaLockResetEvent { User = GetNetEntity(actor) });
                    break;
                case MechCardLockRegisterMessage:
                    RaiseLocalEvent(ent.Owner, new MechCardLockRegisterEvent { User = GetNetEntity(actor) });
                    break;
                case MechCardLockToggleMessage:
                    RaiseLocalEvent(ent.Owner, new MechCardLockToggleEvent { User = GetNetEntity(actor) });
                    break;
                case MechCardLockResetMessage:
                    RaiseLocalEvent(ent.Owner, new MechCardLockResetEvent { User = GetNetEntity(actor) });
                    break;
            }
        }
    }

    private void OnUpdateMechUi(EntityUid uid, MechComponent component, UpdateMechUiEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Update fan energy consumption and gas processing
        var query = EntityQueryEnumerator<MechComponent, MechFanComponent>();
        while (query.MoveNext(out var uid, out var mechComp, out var fanComp))
        {
            if (!fanComp.IsActive)
                continue;

            // Consume energy from battery
            var energyConsumption = fanComp.EnergyConsumption * frameTime;
            if (!TryChangeEnergy(uid, -energyConsumption, mechComp))
            {
                // If not enough energy, turn off fan
                fanComp.IsActive = false;
                Dirty(uid, fanComp);
                continue;
            }
        }
    }

    public override void UpdateUserInterface(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var equipment = new List<NetEntity>();
        foreach (var ent in component.EquipmentContainer.ContainedEntities)
        {
            equipment.Add(GetNetEntity(ent));
        }

        var state = new MechBoundUiState
        {
            Equipment = equipment,
            IsAirtight = component.Airtight,
            FanActive = TryComp<MechFanComponent>(uid, out var fanComp) && fanComp.IsActive,
            CabinGasLevel = TryComp<MechAirComponent>(uid, out var airComp) ?
                airComp.Air.Pressure : 0f
        };

        // Update lock system state if MechLockComponent is present
        if (TryComp<MechLockComponent>(uid, out var lockComp))
        {
            state.DnaLockRegistered = lockComp.DnaLockRegistered;
            state.DnaLockActive = lockComp.DnaLockActive;
            state.CardLockRegistered = lockComp.CardLockRegistered;
            state.CardLockActive = lockComp.CardLockActive;
            state.OwnerDna = lockComp.OwnerDna;
            state.OwnerCardName = lockComp.OwnerCardName;
            state.IsLocked = lockComp.IsLocked;

            var actors = _ui.GetActors(uid, MechUiKey.Key);
            if (actors.Any())
            {
                var user = actors.First();
                state.HasAccess = _lockSystem.HasAccess(user, lockComp);
            }
            else
            {
                state.HasAccess = false;
            }
        }

        _ui.SetUiState(uid, MechUiKey.Key, state);
    }

    public override void BreakMech(EntityUid uid, MechComponent? component = null)
    {
        base.BreakMech(uid, component);

        _ui.CloseUi(uid, MechUiKey.Key);
        _actionBlocker.UpdateCanMove(uid);
    }

    public override bool TryChangeEnergy(EntityUid uid, FixedPoint2 delta, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!base.TryChangeEnergy(uid, delta, component))
            return false;

        var battery = component.BatterySlot.ContainedEntity;
        if (battery == null)
            return false;

        if (!TryComp<BatteryComponent>(battery, out var batteryComp))
            return false;

        _battery.SetCharge(battery!.Value, batteryComp.CurrentCharge + delta.Float(), batteryComp);
        if (batteryComp.CurrentCharge != component.Energy) //if there's a discrepency, we have to resync them
        {
            Log.Debug($"Battery charge was not equal to mech charge. Battery {batteryComp.CurrentCharge}. Mech {component.Energy}");
            component.Energy = batteryComp.CurrentCharge;
            Dirty(uid, component);
        }
        _actionBlocker.UpdateCanMove(uid);
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
        UpdateUserInterface(uid, component);
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
        UpdateUserInterface(uid, component);
    }

    public override bool CanInsert(EntityUid uid, EntityUid toInsert, MechComponent? component = null)
    {
        return base.CanInsert(uid, toInsert, component) && _actionBlocker.CanMove(toInsert);
    }

    #region Atmos Handling
    private void OnInhale(EntityUid uid, MechPilotComponent component, InhaleLocationEvent args)
    {
        if (!TryComp<MechComponent>(component.Mech, out var mech) ||
            !TryComp<MechAirComponent>(component.Mech, out var mechAir))
        {
            return;
        }

        if (mech.Airtight)
            args.Gas = mechAir.Air;

        UpdateUserInterface(component.Mech, mech);
    }

    private void OnExhale(EntityUid uid, MechPilotComponent component, ExhaleLocationEvent args)
    {
        if (!TryComp<MechComponent>(component.Mech, out var mech) ||
            !TryComp<MechAirComponent>(component.Mech, out var mechAir))
        {
            return;
        }

        if (mech.Airtight)
            args.Gas = mechAir.Air;

        UpdateUserInterface(component.Mech, mech);
    }

    private void OnExpose(EntityUid uid, MechPilotComponent component, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(component.Mech, out MechComponent? mech))
            return;

        if (mech.Airtight && TryComp(component.Mech, out MechAirComponent? air))
        {
            args.Handled = true;
            args.Gas = air.Air;
            return;
        }

        args.Gas = _atmosphere.GetContainingMixture(component.Mech, excite: args.Excite);
        args.Handled = true;

        UpdateUserInterface(component.Mech, mech);
    }

    private void OnGetFilterAir(EntityUid uid, MechAirComponent component, ref GetFilterAirEvent args)
    {
        if (args.Air != null)
            return;

        // only airtight mechs get internal air
        if (!TryComp<MechComponent>(uid, out var mech) || !mech.Airtight)
            return;

        args.Air = component.Air;
    }
    #endregion
}
