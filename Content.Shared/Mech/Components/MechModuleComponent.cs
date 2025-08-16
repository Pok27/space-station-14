using Robust.Shared.Serialization;
using Content.Shared.DoAfter;

namespace Content.Shared.Mech.Components;

[RegisterComponent]
public sealed partial class MechModuleComponent : Component
{
    [DataField]
    public int Size = 1;

    /// <summary>
    /// How long it takes to install this passive module
    /// </summary>
    [DataField]
    public float InstallDuration = 5f;
}

[Serializable, NetSerializable]
public sealed partial class InsertModuleEvent : SimpleDoAfterEvent
{
}
