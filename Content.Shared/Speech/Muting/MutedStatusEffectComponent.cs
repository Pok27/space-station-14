namespace Content.Shared.Speech.Muting;

/// <summary>
/// Marks a status effect that prevents speaking, screaming, and vocal emotes.
/// </summary>
[RegisterComponent]
public sealed partial class MutedStatusEffectComponent : Component
{
    /// <summary>
    /// Prototype ID of the muted status effect entity.
    /// </summary>
    public const string StatusEffectPrototype = "StatusEffectMuted";
}
