using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;

namespace Content.Shared.Medical.Disease;

/// <summary>
/// Base class for cure step variants.
/// </summary>
public abstract partial class CureStep
{
    /// <summary>
    /// If true, a successful cure step lowers the current disease stage by 1 instead of curing entirely.
    /// </summary>
    [DataField]
    public bool LowerStage { get; private set; } = false;

    /// <summary>
    /// Attempts to execute this cure step on the given entity.
    /// </summary>
    public virtual bool OnCure(EntityUid uid, DiseasePrototype disease)
    {
        return false;
    }

    /// <summary>
    /// Returns one or more localized lines describing this cure step for diagnoser reports.
    /// </summary>
    public virtual IEnumerable<string> BuildDiagnoserLines(IPrototypeManager prototypes)
    {
        yield break;
    }
}
