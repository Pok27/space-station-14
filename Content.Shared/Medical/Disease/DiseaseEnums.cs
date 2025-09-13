using System;

namespace Content.Shared.Medical.Disease;

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
