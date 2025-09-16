using System.Collections.Generic;

namespace Content.Shared.Medical.Disease;

/// <summary>
/// Extension helpers for working with <see cref="DiseasePrototype"/> on both client and server.
/// </summary>
public static class DiseasePrototypeExtensions
{
    /// <summary>
    /// Returns true if the disease prototype declares the specified spread flag.
    /// Uses the list-based <see cref="DiseasePrototype.SpreadFlags"/> field.
    /// </summary>
    public static bool HasSpreadFlag(this DiseasePrototype? proto, DiseaseSpreadFlags flag)
    {
        if (proto == null)
            return false;

        var flags = proto.SpreadFlags;
        return flags != null && flags.Count > 0 && flags.Contains(flag);
    }
}


