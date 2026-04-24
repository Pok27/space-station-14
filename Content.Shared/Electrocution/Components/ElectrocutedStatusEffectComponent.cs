using Robust.Shared.GameStates;

namespace Content.Shared.Electrocution;

/// <summary>
/// Marks a status effect entity as an active electrocution effect.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ElectrocutedStatusEffectComponent : Component;
