using Content.Shared.Access.Systems;
using Content.Shared.Forensics.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Popups;

namespace Content.Shared.Mech.EntitySystems;

/// <summary>
/// System for managing mech lock functionality (DNA and Card locks)
/// </summary>
public abstract partial class SharedMechLockSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechLockComponent, ComponentStartup>(OnLockStartup);
    }

    private void OnLockStartup(EntityUid uid, MechLockComponent component, ComponentStartup args)
    {
        UpdateLockState(uid, component);
    }

    /// <summary>
    /// Updates the overall lock state based on individual lock states
    /// </summary>
    public void UpdateLockState(EntityUid uid, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var wasLocked = component.IsLocked;
        component.IsLocked = component.DnaLockActive || component.CardLockActive;
        
        if (wasLocked != component.IsLocked)
        {
            Dirty(uid, component);
            var lockEvent = new MechLockStateChangedEvent(component.IsLocked);
            RaiseLocalEvent(uid, lockEvent);
            UpdateMechUI(uid);
        }
    }

    /// <summary>
    /// Attempts to register DNA lock for the specified user
    /// </summary>
    public bool TryRegisterDnaLock(EntityUid uid, EntityUid user, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!TryComp<DnaComponent>(user, out var dnaComp))
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-no-dna"), uid, user);
            return false;
        }

        component.DnaLockRegistered = true;
        component.OwnerDna = dnaComp.DNA;
        Dirty(uid, component);

        _popup.PopupEntity(Loc.GetString("mech-lock-dna-registered"), uid, user);
        UpdateMechUI(uid);
        return true;
    }

    /// <summary>
    /// Toggles DNA lock state
    /// </summary>
    public bool TryToggleDnaLock(EntityUid uid, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!component.DnaLockRegistered)
            return false;

        component.DnaLockActive = !component.DnaLockActive;
        UpdateLockState(uid, component);

        return true;
    }

    /// <summary>
    /// Resets DNA lock system
    /// </summary>
    public bool TryResetDnaLock(EntityUid uid, EntityUid user, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!TryComp<DnaComponent>(user, out var dnaComp) || dnaComp.DNA != component.OwnerDna)
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-access-denied"), uid, user);
            return false;
        }

        component.DnaLockRegistered = false;
        component.DnaLockActive = false;
        component.OwnerDna = null;
        UpdateLockState(uid, component);

        _popup.PopupEntity(Loc.GetString("mech-lock-reset-success"), uid, user);
        UpdateMechUI(uid);
        return true;
    }

    /// <summary>
    /// Attempts to register card lock for the specified user
    /// </summary>
    public bool TryRegisterCardLock(EntityUid uid, EntityUid user, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!_idCard.TryFindIdCard(user, out var idCard))
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-no-card"), uid, user);
            return false;
        }

        component.CardLockRegistered = true;
        component.OwnerCardName = idCard.Comp.FullName;
        Dirty(uid, component);

        _popup.PopupEntity(Loc.GetString("mech-lock-card-registered"), uid, user);
        UpdateMechUI(uid);
        return true;
    }

    /// <summary>
    /// Toggles card lock state
    /// </summary>
    public bool TryToggleCardLock(EntityUid uid, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!component.CardLockRegistered)
            return false;

        component.CardLockActive = !component.CardLockActive;
        UpdateLockState(uid, component);

        return true;
    }

    /// <summary>
    /// Resets card lock system
    /// </summary>
    public bool TryResetCardLock(EntityUid uid, EntityUid user, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!_idCard.TryFindIdCard(user, out var idCard) || idCard.Comp.FullName != component.OwnerCardName)
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-access-denied"), uid, user);
            return false;
        }

        component.CardLockRegistered = false;
        component.CardLockActive = false;
        component.OwnerCardName = null;
        UpdateLockState(uid, component);

        _popup.PopupEntity(Loc.GetString("mech-lock-reset-success"), uid, user);
        UpdateMechUI(uid);
        return true;
    }

    /// <summary>
    /// Checks if a user has access to a locked mech
    /// </summary>
    public bool HasAccess(EntityUid user, MechLockComponent component)
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

    /// <summary>
    /// Shows appropriate lock state message to user
    /// </summary>
    public void ShowLockMessage(EntityUid uid, EntityUid user, MechLockComponent component, bool isActivating)
    {
        var messageKey = isActivating ? "mech-lock-activated" : "mech-lock-deactivated";
        _popup.PopupEntity(Loc.GetString(messageKey), uid, user);
    }

    /// <summary>
    /// Updates mech UI when lock state changes. Override in server systems.
    /// </summary>
    protected virtual void UpdateMechUI(EntityUid uid)
    {
        // Base implementation does nothing - override in server systems
    }
}

/// <summary>
/// Event raised when the mech lock state changes
/// </summary>
public sealed class MechLockStateChangedEvent : EntityEventArgs
{
    public bool IsLocked { get; }

    public MechLockStateChangedEvent(bool isLocked)
    {
        IsLocked = isLocked;
    }
}