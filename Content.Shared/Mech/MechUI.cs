﻿using Robust.Shared.Serialization;
using Content.Shared.Mech.Components;

namespace Content.Shared.Mech;

[Serializable, NetSerializable]
public enum MechUiKey : byte
{
    Key,
    Equipment
}

/// <summary>
/// Fan states for the mech air system
/// </summary>
[Serializable, NetSerializable]
public enum MechFanState : byte
{
    Off,
    On,
    Idle  // Fan is on but not consuming energy due to unsuitable atmosphere
}

/// <summary>
/// Event raised to collect BUI states for each of the mech's equipment items
/// </summary>
public sealed class MechEquipmentUiStateReadyEvent : EntityEventArgs
{
    public Dictionary<NetEntity, BoundUserInterfaceState> States = new();
}

/// <summary>
/// Event raised to relay an equipment ui message
/// </summary>
public sealed class MechEquipmentUiMessageRelayEvent : EntityEventArgs
{
    public MechEquipmentUiMessage Message;

    public MechEquipmentUiMessageRelayEvent(MechEquipmentUiMessage message)
    {
        Message = message;
    }
}

/// <summary>
/// UI event raised to remove a piece of equipment from a mech
/// </summary>
[Serializable, NetSerializable]
public sealed class MechEquipmentRemoveMessage : BoundUserInterfaceMessage
{
    public NetEntity Equipment;

    public MechEquipmentRemoveMessage(NetEntity equipment)
    {
        Equipment = equipment;
    }
}

/// <summary>
/// base for all mech ui messages
/// </summary>
[Serializable, NetSerializable]
public abstract class MechEquipmentUiMessage : BoundUserInterfaceMessage
{
    public NetEntity Equipment;
}

/// <summary>
/// event raised for the grabber equipment to eject an item from it's storage
/// </summary>
[Serializable, NetSerializable]
public sealed class MechGrabberEjectMessage : MechEquipmentUiMessage
{
    public NetEntity Item;

    public MechGrabberEjectMessage(NetEntity equipment, NetEntity uid)
    {
        Equipment = equipment;
        Item = uid;
    }
}

/// <summary>
/// Event raised for the soundboard equipment to play a sound from its component
/// </summary>
[Serializable, NetSerializable]
public sealed class MechSoundboardPlayMessage : MechEquipmentUiMessage
{
    public int Sound;

    public MechSoundboardPlayMessage(NetEntity equipment, int sound)
    {
        Equipment = equipment;
        Sound = sound;
    }
}

/// <summary>
/// Event raised to toggle the airtight mode of a mech
/// </summary>
[Serializable, NetSerializable]
public sealed class MechAirtightMessage : BoundUserInterfaceMessage
{
    public bool IsAirtight;

    public MechAirtightMessage(bool isAirtight)
    {
        IsAirtight = isAirtight;
    }
}

/// <summary>
/// Event raised to toggle the fan state of a mech
/// </summary>
[Serializable, NetSerializable]
public sealed class MechFanToggleMessage : BoundUserInterfaceMessage
{
    public bool IsActive;

    public MechFanToggleMessage(bool isActive)
    {
        IsActive = isActive;
    }
}

/// <summary>
/// Event raised to select equipment in the radial menu
/// </summary>
[Serializable, NetSerializable]
public sealed class MechEquipmentSelectMessage : BoundUserInterfaceMessage
{
    public NetEntity? Equipment;

    public MechEquipmentSelectMessage(NetEntity? equipment)
    {
        Equipment = equipment;
    }
}

/// <summary>
/// BUI state for mechs that also contains all equipment ui states.
/// </summary>
/// <remarks>
///    ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⡠⢐⠤⢃⢰⠐⡄⣀⠀⠀
///    ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⠔⣨⠀⢁⠁⠐⡐⠠⠜⠐⠀
///    ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠔⠐⢀⡁⣀⠔⡌⠡⢀⢐⠁⠀
///    ⠀⠀⠀⠀⢀⠔⠀⡂⡄⠠⢀⡀⠀⣄⡀⠠⠤⠴⡋⠑⡠⠀⠔⠐⢂⠕⢀⡂⠀⠀
///    ⠀⠀⠀⡔⠁⠠⡐⠁⠀⠀⠀⢘⠀⠀⠀⠀⠠⠀⠈⠪⠀⠑⠡⣃⠈⠤⡈⠀⠀⠀
///    ⠀⠀⠨⠀⠄⡒⠀⡂⢈⠀⣀⢌⠀⠀⠁⡈⠀⢆⢀⠀⡀⠉⠒⢆⠑⠀⠀⠀⠀⠀
///    ⠀⠀⠀⡁⠐⠠⠐⡀⠀⢀⣀⠣⡀⠢⡀⠀⢀⡃⠰⠀⠈⠠⢁⠎⠀⠀⠀⠀⠀⠀
///    ⠀⠀⠀⠅⠒⣈⢣⠠⠈⠕⠁⠱⠄⢤⠈⠪⠡⠎⢘⠈⡁⢙⠈⠀⠀⠀⠀⠀⠀⠀
///    ⠀⠀⠀⠃⠀⢡⠀⠧⠀⡇⠀⠀⠀⠀⠀⠀⠀⠀⠀⢕⡈⠌⠀⠀⠀⠀⠀⠀⠀⠀
///    ⠀⠀⠀⠀⠀⠀⠈⡀⡀⡆⠀⠀⠀⠀⠀⠀⠀⠀⠀⡰⠀⡐⠀⠀⠀⠀⠀⠀⠀⠀
///    ⠀⠀⠀⠀⠀⠀⠀⢈⢂⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠸⠀⡃⠀⠀⠀⠀⠀⠀⠀⠀
///    ⠀⠀⠀⠀⠀⠀⠀⠎⠐⢅⠀⠀⠀⠀⠀⠀⠀⠀⠀⢐⠅⠚⠄⠀⠀⠀⠀⠀⠀⠀
///    ⠀⠀⢈⠩⠈⠀⠐⠁⠀⢀⠀⠄⡂⠒⠐⠀⠆⠁⠰⠠⠀⢅⠈⠐⠄⢁⢡⠀⠀⠀
///    ⠀⠀⢈⡀⠰⡁⠀⠁⠴⠁⠔⠀⠀⠄⠄⡁⠀⠂⠀⠢⠠⠁⠀⠠⠈⠂⠬⠀⠀⠀
///    ⠀⠀⠠⡂⢄⠤⠒⣁⠐⢕⢀⡈⡐⡠⠄⢐⠀⠈⠠⠈⡀⠂⢀⣀⠰⠁⠠⠀⠀
/// trojan horse bui state⠀
/// </remarks>
[Serializable, NetSerializable]
public sealed class MechBoundUiState : BoundUserInterfaceState
{
    public List<NetEntity> Equipment = new();
    public bool IsAirtight;
    public bool FanActive;
    public MechFanState FanState = MechFanState.Off;
    public float CabinGasLevel;

    // Lock system
    public bool DnaLockRegistered;
    public bool DnaLockActive;
    public bool CardLockRegistered;
    public bool CardLockActive;
    public string? OwnerDna;
    public string? OwnerCardName;
    public bool IsLocked;
    public bool HasAccess;
}

[Serializable, NetSerializable]
public sealed class MechGrabberUiState : BoundUserInterfaceState
{
    public List<NetEntity> Contents = new();
    public int MaxContents;
}

/// <summary>
/// List of sound collection ids to be localized and displayed.
/// </summary>
[Serializable, NetSerializable]
public sealed class MechSoundboardUiState : BoundUserInterfaceState
{
    public List<string> Sounds = new();
}
