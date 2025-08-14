using System.Linq;
using Content.Shared.Access.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Mech.EntitySystems;

/// <summary>
/// Handles all of the interactions, UI handling, and items shennanigans for <see cref="MechComponent"/>
/// </summary>
public abstract partial class SharedMechSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MechComponent, MechToggleEquipmentEvent>(OnToggleEquipmentAction);
        SubscribeLocalEvent<MechComponent, MechEjectPilotEvent>(OnEjectPilotEvent);
        SubscribeLocalEvent<MechComponent, UserActivateInWorldEvent>(RelayInteractionEvent);
        SubscribeLocalEvent<MechComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MechComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<MechComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
        SubscribeLocalEvent<MechComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<MechComponent, CanDropTargetEvent>(OnCanDragDrop);
        SubscribeLocalEvent<MechComponent, GotEmaggedEvent>(OnEmagged);

        SubscribeLocalEvent<MechPilotComponent, GetMeleeWeaponEvent>(OnGetMeleeWeapon);
        SubscribeLocalEvent<MechPilotComponent, GetActiveWeaponEvent>(OnGetActiveWeapon);
        SubscribeLocalEvent<MechPilotComponent, GetShootingEntityEvent>(OnGetShootingEntity);
        SubscribeLocalEvent<MechPilotComponent, GetProjectileShooterEvent>(OnGetProjectileShooter);
        SubscribeLocalEvent<MechPilotComponent, CanAttackFromContainerEvent>(OnCanAttackFromContainer);
        SubscribeLocalEvent<MechPilotComponent, AttackAttemptEvent>(OnAttackAttempt);

        SubscribeLocalEvent<MechEquipmentComponent, ShotAttemptedEvent>(OnMechEquipmentShotAttempt);
        SubscribeLocalEvent<MechEquipmentComponent, AttemptMeleeEvent>(OnMechEquipmentMeleeAttempt);

        InitializeRelay();
    }

    private void OnToggleEquipmentAction(EntityUid uid, MechComponent component, MechToggleEquipmentEvent args)
    {
        if (args.Handled)
            return;

        if (_net.IsServer)
        {
            args.Handled = true;
        }
        else
        {
            RaiseLocalEvent(uid, new MechOpenEquipmentRadialEvent());
            args.Handled = true;
        }
    }

    private void OnEjectPilotEvent(EntityUid uid, MechComponent component, MechEjectPilotEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        TryEject(uid, component);
    }

    private void RelayInteractionEvent(EntityUid uid, MechComponent component, UserActivateInWorldEvent args)
    {
        var pilot = component.PilotSlot.ContainedEntity;
        if (pilot == null)
            return;

        if (!_timing.IsFirstTimePredicted)
            return;

        if (component.CurrentSelectedEquipment != null)
        {
            RaiseLocalEvent(component.CurrentSelectedEquipment.Value, args);
        }
    }

    private void OnStartup(EntityUid uid, MechComponent component, ComponentStartup args)
    {
        component.PilotSlot = _container.EnsureContainer<ContainerSlot>(uid, component.PilotSlotId);
        component.EquipmentContainer = _container.EnsureContainer<Container>(uid, component.EquipmentContainerId);
        component.BatterySlot = _container.EnsureContainer<ContainerSlot>(uid, component.BatterySlotId);
        component.ModuleContainer = _container.EnsureContainer<Container>(uid, component.ModuleContainerId);
        UpdateAppearance(uid, component);
    }

    private void OnDestruction(EntityUid uid, MechComponent component, DestructionEventArgs args)
    {
        BreakMech(uid, component);
    }

    private void OnGetAdditionalAccess(EntityUid uid, MechComponent component, ref GetAdditionalAccessEvent args)
    {
        var pilot = component.PilotSlot.ContainedEntity;
        if (pilot == null)
            return;

        args.Entities.Add(pilot.Value);
    }

    private void SetupUser(EntityUid mech, EntityUid pilot, MechComponent? component = null)
    {
        if (!Resolve(mech, ref component))
            return;

        var rider = EnsureComp<MechPilotComponent>(pilot);

        // Warning: this bypasses most normal interaction blocking components on the user, like drone laws and the like.
        var irelay = EnsureComp<InteractionRelayComponent>(pilot);

        _mover.SetRelay(pilot, mech);
        _interaction.SetRelay(pilot, mech, irelay);
        rider.Mech = mech;
        Dirty(pilot, rider);

        if (_net.IsClient)
            return;

        _actions.AddAction(pilot, ref component.MechCycleActionEntity, component.MechCycleAction, mech);
        _actions.AddAction(pilot, ref component.MechUiActionEntity, component.MechUiAction, mech);
        _actions.AddAction(pilot, ref component.MechEjectActionEntity, component.MechEjectAction, mech);
    }

    private void RemoveUser(EntityUid mech, EntityUid pilot)
    {
        if (!RemComp<MechPilotComponent>(pilot))
            return;
        RemComp<RelayInputMoverComponent>(pilot);
        RemComp<InteractionRelayComponent>(pilot);

        _actions.RemoveProvidedActions(pilot, mech);
    }

    /// <summary>
    /// Destroys the mech, removing the user and ejecting all installed equipment.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    public virtual void BreakMech(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        TryEject(uid, component);
        var equipment = new List<EntityUid>(component.EquipmentContainer.ContainedEntities);
        foreach (var ent in equipment)
        {
            RemoveEquipment(uid, ent, component, forced: true);
        }

        var modules = new List<EntityUid>(component.ModuleContainer.ContainedEntities);
        foreach (var ent in modules)
        {
            _container.Remove(ent, component.ModuleContainer);
        }

        component.Broken = true;
        UpdateAppearance(uid, component);
    }

    /// <summary>
    /// Removes an equipment item from a mech.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="toRemove"></param>
    /// <param name="component"></param>
    /// <param name="equipmentComponent"></param>
    /// <param name="forced">Whether or not the removal can be cancelled</param>
    public void InsertEquipment(EntityUid uid, EntityUid toInsert, MechComponent? component = null,
        MechEquipmentComponent? equipmentComponent = null, MechModuleComponent? moduleComponent = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // Equipment
        if (Resolve(toInsert, ref equipmentComponent, false))
        {
            if (component.EquipmentContainer.ContainedEntities.Count >= component.MaxEquipmentAmount)
                return;

            if (_whitelistSystem.IsWhitelistFail(component.EquipmentWhitelist, toInsert))
                return;

            equipmentComponent.EquipmentOwner = uid;
            _container.Insert(toInsert, component.EquipmentContainer);
            var ev = new MechEquipmentInsertedEvent(uid);
            RaiseLocalEvent(toInsert, ref ev);
            RaiseLocalEvent(uid, new UpdateMechUiEvent());
            return;
        }

        // Module
        if (Resolve(toInsert, ref moduleComponent, false))
        {
            var used = 0;
            foreach (var ent in component.ModuleContainer.ContainedEntities)
            {
                if (TryComp<MechModuleComponent>(ent, out var m))
                    used += m.Size;
            }
            if (used + moduleComponent.Size > component.MaxModuleAmount)
                return;

            if (_whitelistSystem.IsWhitelistFail(component.ModuleWhitelist, toInsert))
                return;

            _container.Insert(toInsert, component.ModuleContainer);
            var modEv = new MechModuleInsertedEvent(uid);
            RaiseLocalEvent(toInsert, ref modEv);
            RaiseLocalEvent(uid, new UpdateMechUiEvent());
            return;
        }
    }

    /// <summary>
    /// Removes an equipment item from a mech.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="toRemove"></param>
    /// <param name="component"></param>
    /// <param name="equipmentComponent"></param>
    /// <param name="forced">Whether or not the removal can be cancelled</param>
    public void RemoveEquipment(EntityUid uid, EntityUid toRemove, MechComponent? component = null,
        MechEquipmentComponent? equipmentComponent = null, bool forced = false)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!Resolve(toRemove, ref equipmentComponent))
            return;

        if (!forced)
        {
            var attemptev = new AttemptRemoveMechEquipmentEvent();
            RaiseLocalEvent(toRemove, ref attemptev);
            if (attemptev.Cancelled)
                return;
        }

        var ev = new MechEquipmentRemovedEvent(uid);
        RaiseLocalEvent(toRemove, ref ev);

        if (component.CurrentSelectedEquipment == toRemove)
            component.CurrentSelectedEquipment = null;

        equipmentComponent.EquipmentOwner = null;
        _container.Remove(toRemove, component.EquipmentContainer);
        RaiseLocalEvent(uid, new UpdateMechUiEvent());
    }

    /// <summary>
    /// Attempts to change the amount of energy in the mech.
    /// </summary>
    /// <param name="uid">The mech itself</param>
    /// <param name="delta">The change in energy</param>
    /// <param name="component"></param>
    /// <returns>If the energy was successfully changed.</returns>
    public virtual bool TryChangeEnergy(EntityUid uid, FixedPoint2 delta, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (component.Energy + delta < 0)
            return false;

        component.Energy = FixedPoint2.Clamp(component.Energy + delta, 0, component.MaxEnergy);
        Dirty(uid, component);
        RaiseLocalEvent(uid, new UpdateMechUiEvent());
        return true;
    }

    /// <summary>
    /// Sets the integrity of the mech.
    /// </summary>
    /// <param name="uid">The mech itself</param>
    /// <param name="value">The value the integrity will be set at</param>
    /// <param name="component"></param>
    public void SetIntegrity(EntityUid uid, FixedPoint2 value, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Integrity = FixedPoint2.Clamp(value, 0, component.MaxIntegrity);

        if (component.Integrity <= 0)
        {
            BreakMech(uid, component);
        }
        else if (component.Broken)
        {
            component.Broken = false;
            UpdateAppearance(uid, component);
        }

        Dirty(uid, component);
        RaiseLocalEvent(uid, new UpdateMechUiEvent());
    }

    /// <summary>
    /// Checks if the pilot is present
    /// </summary>
    /// <param name="component"></param>
    /// <returns>Whether or not the pilot is present</returns>
    public bool IsEmpty(MechComponent component)
    {
        return component.PilotSlot.ContainedEntity == null;
    }

    /// <summary>
    /// Checks if an entity can be inserted into the mech.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="toInsert"></param>
    /// <param name="component"></param>
    /// <returns></returns>
    public virtual bool CanInsert(EntityUid uid, EntityUid toInsert, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        return IsEmpty(component);
    }

    /// <summary>
    /// Updates the user interface
    /// </summary>
    /// <remarks>
    /// This is defined here so that UI updates can be accessed from shared.
    /// </remarks>
    public virtual void UpdateUserInterface(EntityUid uid, MechComponent? component = null)
    {
    }

    /// <summary>
    /// Attempts to insert a pilot into the mech.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="toInsert"></param>
    /// <param name="component"></param>
    /// <returns>Whether or not the entity was inserted</returns>
    public bool TryInsert(EntityUid uid, EntityUid? toInsert, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (toInsert == null || component.PilotSlot.ContainedEntity == toInsert)
            return false;

        if (!CanInsert(uid, toInsert.Value, component))
            return false;

        SetupUser(uid, toInsert.Value);
        _container.Insert(toInsert.Value, component.PilotSlot);
        UpdateAppearance(uid, component);
        return true;
    }

    /// <summary>
    /// Attempts to eject the current pilot from the mech
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <returns>Whether or not the pilot was ejected.</returns>
    public bool TryEject(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (component.PilotSlot.ContainedEntity == null)
            return false;

        var pilot = component.PilotSlot.ContainedEntity.Value;

        RemoveUser(uid, pilot);
        _container.RemoveEntity(uid, pilot);
        UpdateAppearance(uid, component);
        return true;
    }

    private void OnGetMeleeWeapon(EntityUid uid, MechPilotComponent component, GetMeleeWeaponEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MechComponent>(component.Mech, out var mech))
            return;

        var weapon = mech.CurrentSelectedEquipment ?? component.Mech;
        args.Weapon = weapon;
        args.Handled = true;
    }

    private void OnGetActiveWeapon(EntityUid uid, MechPilotComponent component, ref GetActiveWeaponEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MechComponent>(component.Mech, out var mech))
            return;

        // Use the currently selected equipment if available, otherwise the mech itself
        var weapon = mech.CurrentSelectedEquipment ?? component.Mech;
        args.Weapon = weapon;
        args.Handled = true;
    }

    private void OnGetShootingEntity(EntityUid uid, MechPilotComponent component, ref GetShootingEntityEvent args)
    {
        if (args.Handled)
            return;

        // Use the mech entity for shooting coordinates and physics instead of the pilot
        args.ShootingEntity = component.Mech;
        args.Handled = true;
    }

    private void OnGetProjectileShooter(EntityUid uid, MechPilotComponent component, ref GetProjectileShooterEvent args)
    {
        if (args.Handled)
            return;

        // Use the mech entity as the shooter for projectiles to prevent self-damage
        args.ProjectileShooter = component.Mech;
        args.Handled = true;
    }

    private void OnCanAttackFromContainer(EntityUid uid, MechPilotComponent component, CanAttackFromContainerEvent args)
    {
        args.CanAttack = true;
    }

    private void OnAttackAttempt(EntityUid uid, MechPilotComponent component, AttackAttemptEvent args)
    {
        if (args.Target == component.Mech)
            args.Cancel();
    }

    private void OnMechEquipmentShotAttempt(Entity<MechEquipmentComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!ent.Comp.BlockUseOutsideMech)
            return;

        if (!ent.Comp.EquipmentOwner.HasValue)
            args.Cancel();
    }

    private void OnMechEquipmentMeleeAttempt(Entity<MechEquipmentComponent> ent, ref AttemptMeleeEvent args)
    {
        if (!ent.Comp.BlockUseOutsideMech)
            return;

        var owner = ent.Comp.EquipmentOwner;
        if (owner.HasValue)
            return;

        if (_container.TryGetContainingContainer(ent.Owner, out var container) && HasComp<MechComponent>(container.Owner))
            return;

        args.Cancelled = true;
    }

    private void UpdateAppearance(EntityUid uid, MechComponent? component = null,
        AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref component, ref appearance, false))
            return;

        _appearance.SetData(uid, MechVisuals.Open, IsEmpty(component), appearance);
        _appearance.SetData(uid, MechVisuals.Broken, component.Broken, appearance);
    }

    private void OnDragDrop(EntityUid uid, MechComponent component, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.Dragged, component.EntryDelay, new MechEntryEvent(), uid, target: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnCanDragDrop(EntityUid uid, MechComponent component, ref CanDropTargetEvent args)
    {
        args.Handled = true;

        args.CanDrop |= !component.Broken && CanInsert(uid, args.Dragged, component);
    }

    private void OnEmagged(EntityUid uid, MechComponent component, ref GotEmaggedEvent args)
    {
        if (!component.BreakOnEmag)
            return;
        args.Handled = true;
        component.EquipmentWhitelist = null;
        Dirty(uid, component);
    }
}

/// <summary>
/// Event to request mech UI update (shared between client and server)
/// </summary>
[Serializable, NetSerializable]
public sealed class UpdateMechUiEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed partial class RemoveBatteryEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class MechExitEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class MechEntryEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class RemoveModuleEvent : SimpleDoAfterEvent
{
}

#region Lock Events
[Serializable, NetSerializable]
public sealed partial class MechDnaLockRegisterEvent : EntityEventArgs
{
    public NetEntity User;
}

[Serializable, NetSerializable]
public sealed partial class MechDnaLockToggleEvent : EntityEventArgs
{
    public NetEntity User;
}

[Serializable, NetSerializable]
public sealed partial class MechDnaLockResetEvent : EntityEventArgs
{
    public NetEntity User;
}

[Serializable, NetSerializable]
public sealed partial class MechCardLockRegisterEvent : EntityEventArgs
{
    public NetEntity User;
}

[Serializable, NetSerializable]
public sealed partial class MechCardLockToggleEvent : EntityEventArgs
{
    public NetEntity User;
}

[Serializable, NetSerializable]
public sealed partial class MechCardLockResetEvent : EntityEventArgs
{
    public NetEntity User;
}

[Serializable, NetSerializable]
public sealed partial class MechDnaLockRegisterMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class MechDnaLockToggleMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class MechDnaLockResetMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class MechCardLockRegisterMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class MechCardLockToggleMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed partial class MechCardLockResetMessage : BoundUserInterfaceMessage
{
}
#endregion
