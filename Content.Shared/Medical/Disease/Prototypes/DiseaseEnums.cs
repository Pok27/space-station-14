using System;

namespace Content.Shared.Medical.Disease;

/// <summary>
/// Enumeration describing disease transmission vectors.
/// TODO: only the Contact works and Airborne.
/// </summary>
public enum DiseaseSpreadFlags
{
    None = 0,
    Airborne = 1 << 0,
    Contact = 1 << 1,
    Blood = 1 << 2,
    Special = 1 << 3,
}

/// <summary>
/// Enumeration describing disease stealth behavior flags.
/// TODO:
/// - None: default behavior
/// - Hidden: do not show in HUD
/// - VeryHidden: hide from HUD, diagnoser, and health analyzer
/// - HiddenTreatment: hide treatment steps in diagnoser
/// - HiddenStage: hide stage in diagnoser and health analyzer
/// </summary>
[Flags]
public enum DiseaseStealthFlags
{
    None = 0,
    Hidden = 1 << 0,
    VeryHidden = 1 << 1,
    HiddenTreatment = 1 << 2,
    HiddenStage = 1 << 3,
}
