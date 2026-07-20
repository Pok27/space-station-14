using Robust.Shared.GameStates;

namespace Content.Shared.Speech.Components;

/// <summary>
/// Makes the entity's speech unblockable by speech-blocking effects or entities.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UnblockableSpeechComponent : Component;
