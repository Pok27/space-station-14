using Robust.Shared.GameStates;

namespace Content.Shared.Speech.Components;

/// <summary>
/// ЯussiДи! Becomes incomprehensible to read for anyone who actually knows cyrillic.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RussianAccentComponent : BaseAccentComponent;
