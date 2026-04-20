namespace Content.Shared.Speech.Muting;

/// <summary>
/// Marks a status effect that prevents speaking, screaming, and vocal emotes.
/// </summary>
[RegisterComponent]
public sealed partial class MutedStatusEffectComponent : Component
{
    /// <summary>
    /// Popup shown when speech is blocked.
    /// </summary>
    [DataField]
    public string SpeakPopup = "speech-muted";

    /// <summary>
    /// Popup shown when screaming is blocked.
    /// </summary>
    [DataField]
    public string ScreamPopup = "speech-muted";
}
