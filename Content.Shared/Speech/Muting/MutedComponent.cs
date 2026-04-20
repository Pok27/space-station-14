using Robust.Shared.GameStates;

namespace Content.Shared.Speech.Muting;

/// <summary>
/// Marker component for entities that should permanently receive the muted status effect.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MutedComponent : Component;
