using System.Collections.Generic;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

/// <summary>
/// Runtime per-step state stored in the system.
/// </summary>
public sealed partial class DiseaseCureSystem
{
    private sealed class CureState
    {
        public float Ticker;
    }

    private readonly Dictionary<(EntityUid, string, CureStep), CureState> _cureStates = new();

    /// <summary>
    /// Retrieves the runtime state for the given (entity, disease, step), creating it if missing.
    /// </summary>
    private CureState GetState(EntityUid uid, string diseaseId, CureStep step)
    {
        var key = (uid, diseaseId, step);
        if (!_cureStates.TryGetValue(key, out var state))
        {
            state = new CureState();
            _cureStates[key] = state;
        }
        return state;
    }
}
