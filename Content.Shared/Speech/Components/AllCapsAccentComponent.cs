using Robust.Shared.GameStates;

namespace Content.Shared.Speech.Components;

/// <summary>
/// Marks a speech status effect that transforms spoken text to uppercase.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AllCapsAccentComponent : BaseAccentComponent;
