using Content.Shared.Access.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Popups;
using Content.Shared.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Access;
using System.Linq;
using System.Collections.Generic;

namespace Content.Shared.Mech.EntitySystems;

/// <summary>
/// System for managing mech lock functionality (DNA and Card locks)
/// </summary>
public abstract partial class SharedMechLockSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

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
    /// Checks if user has access without any feedback (for UI/verb visibility)
    /// </summary>
    /// <param name="uid">Mech entity</param>
    /// <param name="user">User trying to access</param>
    /// <param name="component">Lock component (optional)</param>
    /// <returns>True if access granted, false if denied</returns>
    public bool CheckAccess(EntityUid uid, EntityUid user, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return true; // No lock component = no restrictions

        return HasAccess(user, component);
    }

    /// <summary>
    /// Checks if user has access and plays deny sound if not
    /// </summary>
    /// <param name="uid">Mech entity</param>
    /// <param name="user">User trying to access</param>
    /// <param name="component">Lock component (optional)</param>
    /// <returns>True if access granted, false if denied</returns>
    public bool CheckAccessWithFeedback(EntityUid uid, EntityUid user, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return true; // No lock component = no restrictions

        if (HasAccess(user, component))
            return true;

        // Access denied - show popup and play sound
        _popup.PopupEntity(Loc.GetString("mech-lock-access-denied-popup"), uid, user);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/airlock_deny.ogg"), uid, AudioParams.Default.WithVolume(-5f));
        return false;
    }

    /// <summary>
    /// Attempts to register a lock for the specified user
    /// </summary>
    public bool TryRegisterLock(EntityUid uid, EntityUid user, MechLockType lockType, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        // If any lock is already registered, only an existing owner may register additional locks
        var anyRegistered = component.DnaLockRegistered || component.CardLockRegistered;
        if (anyRegistered && !IsAnyOwner(user, component))
        {
            DenyWithFeedback(uid, user);
            return false;
        }

        switch (lockType)
        {
            case MechLockType.Dna:
                if (!TryComp<DnaComponent>(user, out var dnaComp))
                {
                    _popup.PopupEntity(Loc.GetString("mech-lock-no-dna-popup"), uid, user);
                    return false;
                }
                component.DnaLockRegistered = true;
                component.OwnerDna = dnaComp.DNA;
                _popup.PopupEntity(Loc.GetString("mech-lock-dna-registered-popup"), uid, user);
                break;

            case MechLockType.Card:
                if (!TryFindIdCard(user, out var idCard))
                {
                    _popup.PopupEntity(Loc.GetString("mech-lock-no-card-popup"), uid, user);
                    return false;
                }
                component.CardLockRegistered = true;
                component.OwnerJobTitle = idCard.Comp.LocalizedJobTitle;
                if (TryComp<AccessComponent>(idCard.Owner, out var access))
                {
                    component.CardAccessTags = new HashSet<ProtoId<AccessLevelPrototype>>(access.Tags);
                }
                _popup.PopupEntity(Loc.GetString("mech-lock-card-registered-popup"), uid, user);
                break;
        }

        UpdateLockState(uid, component);
        UpdateMechUI(uid);
        return true;
    }

    /// <summary>
    /// Toggles lock state
    /// </summary>
    public bool TryToggleLock(EntityUid uid, EntityUid user, MechLockType lockType, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        // Only the owner of the specific lock type may toggle it
        if (!IsOwnerOfLock(user, lockType, component))
        {
            DenyWithFeedback(uid, user);
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
        UpdateMechUI(uid);
        return true;
    }

    /// <summary>
    /// Resets lock system
    /// </summary>
    public bool TryResetLock(EntityUid uid, EntityUid user, MechLockType lockType, MechLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        // Only the owner of the specific lock type may reset it
        if (!IsOwnerOfLock(user, lockType, component))
        {
            DenyWithFeedback(uid, user);
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
                component.OwnerJobTitle = null;
                component.CardAccessTags = null;
                break;
        }

        UpdateLockState(uid, component);
        UpdateMechUI(uid);
        _popup.PopupEntity(Loc.GetString("mech-lock-reset-success-popup"), uid, user);
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
            // For card, return the job title as the display string
            MechLockType.Card => (component.CardLockRegistered, component.CardLockActive, component.OwnerJobTitle),
            _ => (false, false, null)
        };
    }

    /// <summary>
    /// Checks if a user has access to a locked mech and can manage locks
    /// </summary>
    public bool HasAccess(EntityUid user, MechLockComponent component)
    {
        // If mech is not locked, UI settings are available to anyone
        if (!component.IsLocked)
            return true;

        // If locked, only owners of ACTIVE locks grant access
        foreach (MechLockType lockType in Enum.GetValues<MechLockType>())
        {
            var (isRegistered, isActive, ownerId) = GetLockState(lockType, component);
            if (!isRegistered || !isActive || ownerId == null)
                continue;

            switch (lockType)
            {
                case MechLockType.Dna:
                    if (TryComp<DnaComponent>(user, out var dnaComp) && dnaComp.DNA == ownerId)
                        return true;
                    break;

                case MechLockType.Card:
                    // Compare access tags with those captured during registration
                    if (component.CardAccessTags != null && component.CardAccessTags.Count > 0 && TryFindIdCard(user, out var idCard))
                    {
                        if (TryComp<AccessComponent>(idCard.Owner, out var access))
                        {
                            // Ensure the user's card has at least the registered tags
                            if (component.CardAccessTags.All(tag => access.Tags.Contains(tag)))
                                return true;
                        }
                    }
                    break;
            }
        }

        // If no locks are registered or active, anyone can access
        if (!component.DnaLockRegistered && !component.CardLockRegistered)
            return true;

        return false;
    }

    /// <summary>
    /// Shows appropriate lock state message to user
    /// </summary>
    public void ShowLockMessage(EntityUid uid, EntityUid user, MechLockComponent component, bool isActivating)
    {
        var messageKey = isActivating ? "mech-lock-activated-popup" : "mech-lock-deactivated-popup";
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

    private bool IsOwnerOfLock(EntityUid user, MechLockType lockType, MechLockComponent component)
    {
        var (isRegistered, _, ownerId) = GetLockState(lockType, component);
        if (!isRegistered || ownerId == null)
            return false;
        switch (lockType)
        {
            case MechLockType.Dna:
                return TryComp<DnaComponent>(user, out var dnaComp) && dnaComp.DNA == ownerId;
            case MechLockType.Card:
                if (component.CardAccessTags == null || component.CardAccessTags.Count == 0)
                    return false;
                if (!TryFindIdCard(user, out var idCard))
                    return false;
                if (!TryComp<AccessComponent>(idCard.Owner, out var access))
                    return false;
                return component.CardAccessTags.All(tag => access.Tags.Contains(tag));
        }
        return false;
    }

    private bool IsAnyOwner(EntityUid user, MechLockComponent component)
    {
        return IsOwnerOfLock(user, MechLockType.Dna, component) || IsOwnerOfLock(user, MechLockType.Card, component);
    }

    private void DenyWithFeedback(EntityUid uid, EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("mech-lock-access-denied-popup"), uid, user);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/airlock_deny.ogg"), uid, AudioParams.Default.WithVolume(-5f));
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
