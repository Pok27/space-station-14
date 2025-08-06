using Content.Server.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Popups;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Event to request mech UI update (server-side only)
/// </summary>
public sealed class UpdateMechUiEvent : EntityEventArgs
{
}

/// <summary>
/// Server-side system for mech lock functionality
/// </summary>
public sealed class MechLockSystem : SharedMechLockSystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechLockComponent, MechDnaLockRegisterEvent>(OnDnaLockRegister);
        SubscribeLocalEvent<MechLockComponent, MechDnaLockToggleEvent>(OnDnaLockToggle);
        SubscribeLocalEvent<MechLockComponent, MechDnaLockResetEvent>(OnDnaLockReset);
        SubscribeLocalEvent<MechLockComponent, MechCardLockRegisterEvent>(OnCardLockRegister);
        SubscribeLocalEvent<MechLockComponent, MechCardLockToggleEvent>(OnCardLockToggle);
        SubscribeLocalEvent<MechLockComponent, MechCardLockResetEvent>(OnCardLockReset);
    }

    private void OnDnaLockRegister(EntityUid uid, MechLockComponent component, MechDnaLockRegisterEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        TryRegisterDnaLock(uid, user, component);
    }

    private void OnDnaLockToggle(EntityUid uid, MechLockComponent component, MechDnaLockToggleEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        if (TryToggleDnaLock(uid, component))
        {
            ShowLockMessage(uid, user, component, component.DnaLockActive);
        }
    }

    private void OnDnaLockReset(EntityUid uid, MechLockComponent component, MechDnaLockResetEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        TryResetDnaLock(uid, user, component);
    }

    private void OnCardLockRegister(EntityUid uid, MechLockComponent component, MechCardLockRegisterEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        TryRegisterCardLock(uid, user, component);
    }

    private void OnCardLockToggle(EntityUid uid, MechLockComponent component, MechCardLockToggleEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        if (TryToggleCardLock(uid, component))
        {
            ShowLockMessage(uid, user, component, component.CardLockActive);
        }
    }

    private void OnCardLockReset(EntityUid uid, MechLockComponent component, MechCardLockResetEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        TryResetCardLock(uid, user, component);
    }

    protected override void UpdateMechUI(EntityUid uid)
    {
        // Forward to MechSystem for UI update
        var ev = new UpdateMechUiEvent();
        RaiseLocalEvent(uid, ev);
    }

    protected override bool TryFindIdCard(EntityUid user, out Entity<IdCardComponent> idCard)
    {
        return _idCard.TryFindIdCard(user, out idCard);
    }
}