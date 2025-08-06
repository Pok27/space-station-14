using Content.Server.Access.Systems;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Server-side system for mech lock functionality
/// </summary>
public sealed class MechLockSystem : SharedMechLockSystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechLockComponent, MechDnaLockRegisterEvent>(OnDnaLockRegister);
        SubscribeLocalEvent<MechLockComponent, MechDnaLockToggleEvent>(OnDnaLockToggle);
        SubscribeLocalEvent<MechLockComponent, MechDnaLockResetEvent>(OnDnaLockReset);
        SubscribeLocalEvent<MechLockComponent, MechCardLockRegisterEvent>(OnCardLockRegister);
        SubscribeLocalEvent<MechLockComponent, MechCardLockToggleEvent>(OnCardLockToggle);
        SubscribeLocalEvent<MechLockComponent, MechCardLockResetEvent>(OnCardLockReset);
        
        // Listen to lock state changes to update UI
        SubscribeLocalEvent<MechLockComponent, MechLockStateChangedEvent>(OnLockStateChanged);
    }

    private void OnDnaLockRegister(EntityUid uid, MechLockComponent component, MechDnaLockRegisterEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        if (TryRegisterDnaLock(uid, user, component))
        {
            UpdateMechUI(uid);
        }
    }

    private void OnDnaLockToggle(EntityUid uid, MechLockComponent component, MechDnaLockToggleEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        if (TryToggleDnaLock(uid, component))
        {
            ShowLockMessage(uid, user, component, component.DnaLockActive);
            UpdateMechUI(uid);
        }
    }

    private void OnDnaLockReset(EntityUid uid, MechLockComponent component, MechDnaLockResetEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        if (TryResetDnaLock(uid, user, component))
        {
            UpdateMechUI(uid);
        }
    }

    private void OnCardLockRegister(EntityUid uid, MechLockComponent component, MechCardLockRegisterEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        if (TryRegisterCardLock(uid, user, component))
        {
            UpdateMechUI(uid);
        }
    }

    private void OnCardLockToggle(EntityUid uid, MechLockComponent component, MechCardLockToggleEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        if (TryToggleCardLock(uid, component))
        {
            ShowLockMessage(uid, user, component, component.CardLockActive);
            UpdateMechUI(uid);
        }
    }

    private void OnCardLockReset(EntityUid uid, MechLockComponent component, MechCardLockResetEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        if (TryResetCardLock(uid, user, component))
        {
            UpdateMechUI(uid);
        }
    }

    private void OnLockStateChanged(EntityUid uid, MechLockComponent component, MechLockStateChangedEvent args)
    {
        UpdateMechUI(uid);
    }

    private void UpdateMechUI(EntityUid uid)
    {
        // Forward to MechSystem for UI update
        var ev = new UpdateMechUiEvent();
        RaiseLocalEvent(uid, ev);
    }
}