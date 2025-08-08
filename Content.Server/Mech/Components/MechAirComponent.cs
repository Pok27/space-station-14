using Content.Server.Atmos;
using Content.Shared.Atmos;
using Content.Shared.Mech.Components;

namespace Content.Server.Mech.Components;

[RegisterComponent]
public sealed partial class MechAirComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public GasMixture Air = new(0f);

    public void SetVolume(float volume)
    {
        var newMix = new GasMixture(volume);
        newMix.CopyFrom(Air);
        Air = newMix;
    }
}
