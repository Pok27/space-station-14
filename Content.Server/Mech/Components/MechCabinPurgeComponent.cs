using Robust.Shared.GameStates;

namespace Content.Server.Mech.Components;

[RegisterComponent]
public sealed partial class MechCabinPurgeComponent : Component
{
    [DataField]
    public float CooldownRemaining;
}
