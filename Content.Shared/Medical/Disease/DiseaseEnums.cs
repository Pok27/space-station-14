using System;

namespace Content.Shared.Medical.Disease;

/// <summary>
/// Severity band for diseases. Rough guidance for effects and medical response.
/// </summary>
public enum DiseaseSeverity
{
    Minor,
    Moderate,
    Severe,
}

/// <summary>
/// Enumeration describing disease transmission vectors.
/// </summary>
public enum DiseaseSpreadFlags
{
    None = 0,
    Airborne = 1 << 0,
    Contact = 1 << 1,
    Blood = 1 << 2,
    Special = 1 << 3,
}
