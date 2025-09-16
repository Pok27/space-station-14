namespace Content.Shared.Medical.Disease;

[DataDefinition]
public sealed partial class CureTime : CureStep
{
    /// <summary>
    /// Seconds since infection required before curing can occur.
    /// </summary>
    [DataField]
    public float RequiredSeconds { get; private set; } = 90.0f;

    /// <summary>
    /// Chance to cure when the required time elapses (0-1).
    /// </summary>
    [DataField]
    public float CureChance { get; private set; } = 1.0f;
}
