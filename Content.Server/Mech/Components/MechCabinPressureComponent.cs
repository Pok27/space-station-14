using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Server.Mech.Components;

[RegisterComponent]
public sealed partial class MechCabinPressureComponent : Component
{
    /// <summary>
    /// Target pressure for the mech cabin (kPa).
    /// </summary>
    [DataField]
    public float TargetPressure = Atmospherics.OneAtmosphere; // ~101.3 kPa

    /// <summary>
    /// Internal cabin air mixture separate from any attached gas cylinder.
    /// </summary>
    [DataField]
    public GasMixture Air { get; set; } = new(50f);
}
