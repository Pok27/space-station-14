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
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;

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
        SubscribeLocalEvent<MechComponent, MechDnaLockRegisterEvent>(OnDnaLockRegister);
        SubscribeLocalEvent<MechComponent, MechDnaLockToggleEvent>(OnDnaLockToggle);
        SubscribeLocalEvent<MechComponent, MechDnaLockResetEvent>(OnDnaLockReset);
        SubscribeLocalEvent<MechComponent, MechCardLockRegisterEvent>(OnCardLockRegister);
        SubscribeLocalEvent<MechComponent, MechCardLockToggleEvent>(OnCardLockToggle);
        SubscribeLocalEvent<MechComponent, MechCardLockResetEvent>(OnCardLockReset);

        SubscribeLocalEvent<MechComponent, BoundUserInterfaceMessageAttempt>(OnBoundUIAttempt);
        #endregion
    }

    private void OnMechCanMoveEvent(EntityUid uid, MechComponent component, UpdateCanMoveEvent args)
    {
        if (component.Broken || component.Integrity <= 0 || component.Energy <= 0)
            args.Cancel();

        // Check if mech is locked and pilot doesn't have access
        if (component.IsLocked && component.PilotSlot.ContainedEntity != null)
        {
            if (!HasAccess(component.PilotSlot.ContainedEntity.Value, component))
            {
                args.Cancel();
            }
        }
    }

    private void OnInteractUsing(EntityUid uid, MechComponent component, InteractUsingEvent args)
    {
        // Check if mech is locked and user doesn't have access
        if (component.IsLocked && !HasAccess(args.User, component))
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
        if (component.IsLocked && !HasAccess(user, component))
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

    private void OnToolUseAttempt(EntityUid uid, MechPilotComponent component, ref ToolUserAttemptUseEvent args)
    {
        if (args.Target == component.Mech)
            args.Cancelled = true;

        // Check if mech is locked and pilot doesn't have access
        if (TryComp<MechComponent>(component.Mech, out var mechComp) &&
            mechComp.IsLocked && !HasAccess(uid, mechComp))
        {
            args.Cancelled = true;
        }
    }

    private void OnAlternativeVerb(EntityUid uid, MechComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || component.Broken)
            return;

        // Check if mech is locked and user doesn't have access
        if (component.IsLocked && !HasAccess(args.User, component))
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
        if (component.IsLocked && !HasAccess(args.User, component))
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

    #region Lock System
    private void OnDnaLockRegister(EntityUid uid, MechComponent component, MechDnaLockRegisterEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        // Check if user has DNA
        if (!TryComp<DnaComponent>(user, out var dnaComp))
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-no-dna"), uid, user);
            return;
        }

        // Register DNA lock
        component.DnaLockRegistered = true;
        component.OwnerDna = dnaComp.DNA;
        Dirty(uid, component);

        _popup.PopupEntity(Loc.GetString("mech-lock-dna-registered"), uid, user);
        UpdateUserInterface(uid, component);
    }

    private void OnDnaLockToggle(EntityUid uid, MechComponent component, MechDnaLockToggleEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        // Toggle DNA lock
        component.DnaLockActive = !component.DnaLockActive;

        // Update locked status
        component.IsLocked = component.DnaLockActive || component.CardLockActive;
        Dirty(uid, component);

        if (component.DnaLockActive)
            _popup.PopupEntity(Loc.GetString("mech-lock-activated"), uid, user);
        else
            _popup.PopupEntity(Loc.GetString("mech-lock-deactivated"), uid, user);
        UpdateUserInterface(uid, component);
    }

    private void OnCardLockRegister(EntityUid uid, MechComponent component, MechCardLockRegisterEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        // Check if user has ID card
        if (!_idCard.TryFindIdCard(user, out var idCard))
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-no-card"), uid, user);
            return;
        }

        // Register card lock
        component.CardLockRegistered = true;
        component.OwnerCardName = idCard.Comp.FullName;
        Dirty(uid, component);

        _popup.PopupEntity(Loc.GetString("mech-lock-card-registered"), uid, user);
        UpdateUserInterface(uid, component);
    }

    private void OnCardLockToggle(EntityUid uid, MechComponent component, MechCardLockToggleEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        // Toggle card lock
        component.CardLockActive = !component.CardLockActive;

        // Update locked status
        component.IsLocked = component.DnaLockActive || component.CardLockActive;
        Dirty(uid, component);

        if (component.CardLockActive)
            _popup.PopupEntity(Loc.GetString("mech-lock-activated"), uid, user);
        else
            _popup.PopupEntity(Loc.GetString("mech-lock-deactivated"), uid, user);
        UpdateUserInterface(uid, component);
    }

    private void OnDnaLockReset(EntityUid uid, MechComponent component, MechDnaLockResetEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        // Check if user is the original registrator
        if (!TryComp<DnaComponent>(user, out var dnaComp) || dnaComp.DNA != component.OwnerDna)
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-access-denied"), uid, user);
            return;
        }

        // Reset DNA lock completely
        component.DnaLockRegistered = false;
        component.DnaLockActive = false;
        component.OwnerDna = null;

        // Update locked status
        component.IsLocked = component.CardLockActive;
        Dirty(uid, component);

        _popup.PopupEntity(Loc.GetString("mech-lock-reset-success"), uid, user);
        UpdateUserInterface(uid, component);
    }

    private void OnCardLockReset(EntityUid uid, MechComponent component, MechCardLockResetEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        // Check if user is the original registrator
        if (!_idCard.TryFindIdCard(user, out var idCard) || idCard.Comp.FullName != component.OwnerCardName)
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-access-denied"), uid, user);
            return;
        }

        // Reset card lock completely
        component.CardLockRegistered = false;
        component.CardLockActive = false;
        component.OwnerCardName = null;

        // Update locked status
        component.IsLocked = component.DnaLockActive;
        Dirty(uid, component);

        _popup.PopupEntity(Loc.GetString("mech-lock-reset-success"), uid, user);
        UpdateUserInterface(uid, component);
    }

    private void OnBoundUIAttempt(Entity<MechComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.UiKey is not MechUiKey.Key)
            return;

        var actor = args.Actor;
        var message = args.Message;

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

    /// <summary>
    /// Checks if a user has access to a locked mech
    /// </summary>
    public bool HasAccess(EntityUid user, MechComponent component)
    {
        if (!component.IsLocked)
            return true;

        // Check DNA lock
        if (component.DnaLockActive && component.OwnerDna != null)
        {
            if (TryComp<DnaComponent>(user, out var dnaComp) && dnaComp.DNA == component.OwnerDna)
                return true;
        }

        // Check card lock
        if (component.CardLockActive && component.OwnerCardName != null)
        {
            if (_idCard.TryFindIdCard(user, out var idCard) && idCard.Comp.FullName == component.OwnerCardName)
                return true;
        }

        return false;
    }
    #endregion

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
            DnaLockRegistered = component.DnaLockRegistered,
            DnaLockActive = component.DnaLockActive,
            CardLockRegistered = component.CardLockRegistered,
            CardLockActive = component.CardLockActive,
            OwnerDna = component.OwnerDna,
            OwnerCardName = component.OwnerCardName,
            IsLocked = component.IsLocked
        };
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
