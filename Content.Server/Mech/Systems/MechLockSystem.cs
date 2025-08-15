using Content.Server.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Ninja.Components;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Containers;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Server-side system for mech lock functionality
/// </summary>
public sealed class MechLockSystem : SharedMechLockSystem
{
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechLockComponent, MechDnaLockRegisterEvent>(OnDnaLockRegister);
        SubscribeLocalEvent<MechLockComponent, MechDnaLockToggleEvent>(OnDnaLockToggle);
        SubscribeLocalEvent<MechLockComponent, MechDnaLockResetEvent>(OnDnaLockReset);
        SubscribeLocalEvent<MechLockComponent, MechCardLockRegisterEvent>(OnCardLockRegister);
        SubscribeLocalEvent<MechLockComponent, MechCardLockToggleEvent>(OnCardLockToggle);
        SubscribeLocalEvent<MechLockComponent, MechCardLockResetEvent>(OnCardLockReset);
        SubscribeLocalEvent<MechLockComponent, InteractUsingEvent>(OnInteractUsing);
    }

    /// <summary>
    /// Handles DNA lock registration
    /// </summary>
    private void OnDnaLockRegister(EntityUid uid, MechLockComponent component, MechDnaLockRegisterEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        if (TryRegisterLock(uid, user, MechLockType.Dna, component))
        {
            var (_, isActive, _) = GetLockState(MechLockType.Dna, component);
            ShowLockMessage(uid, user, component, isActive);
        }
    }

    /// <summary>
    /// Handles DNA lock toggle
    /// </summary>
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

    /// <summary>
    /// Handles DNA lock reset
    /// </summary>
    private void OnDnaLockReset(EntityUid uid, MechLockComponent component, MechDnaLockResetEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        TryResetLock(uid, user, MechLockType.Dna, component);
    }

    /// <summary>
    /// Handles card lock registration
    /// </summary>
    private void OnCardLockRegister(EntityUid uid, MechLockComponent component, MechCardLockRegisterEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        TryRegisterLock(uid, user, MechLockType.Card, component);
    }

    /// <summary>
    /// Handles card lock toggle
    /// </summary>
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

    /// <summary>
    /// Handles card lock reset
    /// </summary>
    private void OnCardLockReset(EntityUid uid, MechLockComponent component, MechCardLockResetEvent args)
    {
        var user = GetEntity(args.User);
        if (user == EntityUid.Invalid)
            return;

        TryResetLock(uid, user, MechLockType.Card, component);
    }

    protected override void UpdateMechUI(EntityUid uid)
    {
        var ev = new UpdateMechUiEvent();
        RaiseLocalEvent(uid, ev);
    }

    protected override bool TryFindIdCard(EntityUid user, out Entity<IdCardComponent> idCard)
    {
        return _idCard.TryFindIdCard(user, out idCard);
    }

    /// <summary>
    /// Handles AccessBreaker interaction with mech locks
    /// </summary>
    private void OnInteractUsing(EntityUid uid, MechLockComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Check if the user has AccessBreaker
        if (!HasComp<EmagProviderComponent>(args.Used))
            return;

        // Check if the mech is locked
        if (!component.IsLocked)
            return;

        // Check if the mech is immune to AccessBreaker
        if (HasComp<MechComponent>(uid) && _tag.HasTag(uid, "AccessBreakerImmune"))
            return;

        args.Handled = true;

        // Break all locks
        if (component.DnaLockActive)
        {
            component.DnaLockActive = false;
        }

        if (component.CardLockActive)
        {
            component.CardLockActive = false;
        }

        component.IsLocked = false;
        Dirty(uid, component);
    }
}
