namespace Content.Shared.Mech.Components;

[RegisterComponent]
public sealed partial class MechGasCylinderModuleComponent : Component
{
    /// <summary>
    /// Internal gas volume (liters) provided by this module when installed.
    /// </summary>
    [DataField]
    public float TankVolume = 70f;
}
