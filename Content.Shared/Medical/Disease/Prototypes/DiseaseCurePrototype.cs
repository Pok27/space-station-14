using Robust.Shared.Prototypes;

/// <summary>
/// Base class for cure step variants.
/// </summary>
public abstract partial class CureStep
{
    /// <summary>
    /// Returns one or more localized lines describing this cure step for diagnoser reports.
    /// </summary>
    public virtual IEnumerable<string> BuildDiagnoserLines(IPrototypeManager prototypes)
    {
        yield break;
    }
}
