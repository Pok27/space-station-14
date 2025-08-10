using Content.Server.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Popups;

namespace Content.Server.Mech.Systems;



/// <summary>
/// Server-side system for mech lock functionality
/// </summary>
public sealed class MechLockSystem : SharedMechLockSystem
{
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
        Logger.Info($"Received MechDnaLockRegisterEvent for mech {uid}");
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
        {
            Logger.Warning($"Invalid user entity in MechDnaLockRegisterEvent for mech {uid}");
            return;
        }

        Logger.Info($"Registering DNA lock for mech {uid} with user {user}");
        TryRegisterLock(uid, user, MechLockType.Dna, component);
    }

    private void OnDnaLockToggle(EntityUid uid, MechLockComponent component, MechDnaLockToggleEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        if (TryToggleLock(uid, user, MechLockType.Dna, component))
        {
            var (_, isActive, _) = GetLockState(MechLockType.Dna, component);
            ShowLockMessage(uid, user, component, isActive);
        }
    }

    private void OnDnaLockReset(EntityUid uid, MechLockComponent component, MechDnaLockResetEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        TryResetLock(uid, user, MechLockType.Dna, component);
    }

    private void OnCardLockRegister(EntityUid uid, MechLockComponent component, MechCardLockRegisterEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        TryRegisterLock(uid, user, MechLockType.Card, component);
    }

    private void OnCardLockToggle(EntityUid uid, MechLockComponent component, MechCardLockToggleEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        if (TryToggleLock(uid, user, MechLockType.Card, component))
        {
            var (_, isActive, _) = GetLockState(MechLockType.Card, component);
            ShowLockMessage(uid, user, component, isActive);
        }
    }

    private void OnCardLockReset(EntityUid uid, MechLockComponent component, MechCardLockResetEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        TryResetLock(uid, user, MechLockType.Card, component);
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
