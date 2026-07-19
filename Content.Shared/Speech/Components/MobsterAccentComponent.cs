using Robust.Shared.GameStates;

namespace Content.Shared.Speech.Components;

/// <summary>
/// Nyehh, my gabagool, see?
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MobsterAccentComponent : BaseAccentComponent
{
    /// <summary>
    /// Do you make all the rules?
    /// </summary>
    [DataField]
    public bool IsBoss = true;
}
