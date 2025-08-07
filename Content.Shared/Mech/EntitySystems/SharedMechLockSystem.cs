using Content.Shared.Access.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Popups;
using System.Linq;

namespace Content.Shared.Mech.EntitySystems;

/// <summary>
/// System for managing mech lock functionality (DNA and Card locks)
/// </summary>
public abstract partial class SharedMechLockSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

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
        }
    }

    /// <summary>
    /// Attempts to register a lock for the specified user
    /// </summary>
    public bool TryRegisterLock(EntityUid uid, EntityUid user, MechLockType lockType, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        // Check if user has access to manage locks
        if (!HasAccess(user, component))
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-access-denied"), uid, user);
            return false;
        }

        switch (lockType)
        {
            case MechLockType.Dna:
                if (!TryComp<DnaComponent>(user, out var dnaComp))
                {
                    _popup.PopupEntity(Loc.GetString("mech-lock-no-dna"), uid, user);
                    return false;
                }
                component.DnaLockRegistered = true;
                component.OwnerDna = dnaComp.DNA;
                _popup.PopupEntity(Loc.GetString("mech-lock-dna-registered"), uid, user);
                break;

            case MechLockType.Card:
                if (!TryFindIdCard(user, out var idCard))
                {
                    _popup.PopupEntity(Loc.GetString("mech-lock-no-card"), uid, user);
                    return false;
                }
                component.CardLockRegistered = true;
                component.OwnerCardName = idCard.Comp.FullName;
                _popup.PopupEntity(Loc.GetString("mech-lock-card-registered"), uid, user);
                break;
        }

        Dirty(uid, component);
        return true;
    }

    /// <summary>
    /// Toggles lock state
    /// </summary>
    public bool TryToggleLock(EntityUid uid, EntityUid user, MechLockType lockType, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        // Check if user has access to manage locks
        if (!HasAccess(user, component))
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-access-denied"), uid, user);
            return false;
        }

        switch (lockType)
        {
            case MechLockType.Dna:
                if (!component.DnaLockRegistered)
                    return false;
                component.DnaLockActive = !component.DnaLockActive;
                break;

            case MechLockType.Card:
                if (!component.CardLockRegistered)
                    return false;
                component.CardLockActive = !component.CardLockActive;
                break;
        }

        UpdateLockState(uid, component);
        return true;
    }

    /// <summary>
    /// Resets lock system
    /// </summary>
    public bool TryResetLock(EntityUid uid, EntityUid user, MechLockType lockType, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        // Check if user has access to manage locks
        if (!HasAccess(user, component))
        {
            _popup.PopupEntity(Loc.GetString("mech-lock-access-denied"), uid, user);
            return false;
        }

        switch (lockType)
        {
            case MechLockType.Dna:
                component.DnaLockRegistered = false;
                component.DnaLockActive = false;
                component.OwnerDna = null;
                break;

            case MechLockType.Card:
                component.CardLockRegistered = false;
                component.CardLockActive = false;
                component.OwnerCardName = null;
                break;
        }

        UpdateLockState(uid, component);
        _popup.PopupEntity(Loc.GetString("mech-lock-reset-success"), uid, user);
        return true;
    }

    /// <summary>
    /// Gets lock state for a specific lock type
    /// </summary>
    public (bool IsRegistered, bool IsActive, string? OwnerId) GetLockState(MechLockType lockType, MechLockComponent component)
    {
        return lockType switch
        {
            MechLockType.Dna => (component.DnaLockRegistered, component.DnaLockActive, component.OwnerDna),
            MechLockType.Card => (component.CardLockRegistered, component.CardLockActive, component.OwnerCardName),
            _ => (false, false, null)
        };
    }

    /// <summary>
    /// Checks if a user has access to a locked mech and can manage locks
    /// </summary>
    public bool HasAccess(EntityUid user, MechLockComponent component)
    {
        // Check if user has access through any registered lock (active or not)
        foreach (MechLockType lockType in Enum.GetValues<MechLockType>())
        {
            var (isRegistered, _, ownerId) = GetLockState(lockType, component);
            if (isRegistered && ownerId != null)
            {
                switch (lockType)
                {
                    case MechLockType.Dna:
                        if (TryComp<DnaComponent>(user, out var dnaComp) && dnaComp.DNA == ownerId)
                            return true;
                        break;

                    case MechLockType.Card:
                        if (TryFindIdCard(user, out var idCard) && idCard.Comp.FullName == ownerId)
                            return true;
                        break;
                }
            }
        }

        // If no locks are registered, anyone can access
        if (!component.DnaLockRegistered && !component.CardLockRegistered)
            return true;

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

    /// <summary>
    /// Tries to find an ID card. Override in server systems.
    /// </summary>
    protected virtual bool TryFindIdCard(EntityUid user, out Entity<IdCardComponent> idCard)
    {
        idCard = default;
        return false;
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
