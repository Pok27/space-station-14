using Content.Server.Atmos;
using Content.Shared.Atmos;

namespace Content.Server.Mech.Components;

[RegisterComponent]
public sealed partial class MechAirComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public GasMixture Air = new(70f);

    public void SetVolume(float volume)
    {
        var newMix = new GasMixture(volume);
        newMix.CopyFrom(Air);
        Air = newMix;
    }
}
