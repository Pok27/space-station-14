using Content.Shared.FixedPoint;
namespace Content.Shared.Medical.Disease;

/// <summary>
/// Base class for cure step variants. Mirrors symptom behavior style so prototypes can
/// list cure steps inline as typed entries.
/// </summary>
public abstract partial class CureStep
{
}

[DataDefinition]
public sealed partial class CureReagent : CureStep
{
    /// <summary>
    /// Reagent prototype id required to apply this cure step.
    /// </summary>
    [DataField(required: true)]
    public string ReagentId { get; private set; } = string.Empty;

    /// <summary>
    /// Amount of reagent units required to cure.
    /// </summary>
    [DataField]
    public FixedPoint2 Quantity { get; private set; } = FixedPoint2.New(1);
}

[DataDefinition]
public sealed partial class CureSleep : CureStep
{
    /// <summary>
    /// Required amount of accumulated sleep seconds to cure the disease.
    /// If zero or negative, this cure step is ignored.
    /// </summary>
    [DataField]
    public float RequiredSleepSeconds { get; private set; } = 0f;

    /// <summary>
    /// How many seconds of sleep accumulation are removed per second while awake (decay).
    /// </summary>
    [DataField]
    public float AwakeDecayPerSecond { get; private set; } = 0f;
}

[DataDefinition]
public sealed partial class CureTemperature : CureStep
{
    [DataField]
    public float MinTemperature { get; private set; } = 273.15f;

    [DataField]
    public float MaxTemperature { get; private set; } = 310.15f;

    /// <summary>
    /// How many consecutive seconds the temperature must remain inside the range to trigger cure.
    /// </summary>
    [DataField]
    public float RequiredSeconds { get; private set; } = 15f;

    /// <summary>
    /// Chance to cure when the required period is reached (0-1).
    /// If the roll fails, the timer resets.
    /// </summary>
    [DataField]
    public float CureChance { get; private set; } = 1.0f;
}

[DataDefinition]
public sealed partial class CureTime : CureStep
{
    /// <summary>
    /// How many seconds must pass since infection for this cure step to become eligible.
    /// </summary>
    [DataField]
    public float RequiredSeconds { get; private set; } = 90.0f;

    /// <summary>
    /// Chance to cure when the required time is reached (0-1). If roll fails, timer restarts.
    /// </summary>
    [DataField]
    public float CureChance { get; private set; } = 1.0f;
}
