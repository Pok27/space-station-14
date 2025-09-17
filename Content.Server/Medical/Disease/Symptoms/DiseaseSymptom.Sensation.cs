using Content.Shared.Popups;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

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

public sealed partial class DiseaseSymptomSystem
{
    /// <summary>
    /// Shows a small popup to the carrier with the configured localization key.
    /// </summary>
    private void DoSensation(Entity<DiseaseCarrierComponent> ent, SymptomSensation sense)
    {
        var text = Loc.GetString(sense.Popup);
        if (string.IsNullOrEmpty(text))
            return;

        _popup.PopupEntity(text, ent, sense.PopupType);
    }
}


