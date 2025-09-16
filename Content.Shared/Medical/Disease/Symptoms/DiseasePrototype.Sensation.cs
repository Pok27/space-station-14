using Content.Shared.Popups;

namespace Content.Shared.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomSensation : SymptomBehavior
{
    /// <summary>
    /// Localization key for the popup text.
    /// </summary>
    [DataField]
    public string Popup { get; private set; } = string.Empty;

    /// <summary>
    /// Popup visual style.
    /// </summary>
    [DataField]
    public PopupType PopupType { get; private set; } = PopupType.Small;
}
