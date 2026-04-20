using Robust.Shared.GameStates;

namespace Content.Shared.Speech.Muting;

/// <summary>
/// Prevents an entity from speaking, screaming, and producing vocal emotes.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MutedComponent : Component;
