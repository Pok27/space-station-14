using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

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
