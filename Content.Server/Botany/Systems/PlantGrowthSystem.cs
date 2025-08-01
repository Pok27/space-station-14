using Content.Server.Botany.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Botany.Systems;

[ByRefEvent]
public readonly record struct OnPlantGrowEvent;

/// <summary>
/// Handles the main growth cycle for all plants.
/// </summary>
public sealed class PlantGrowthCycleSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public TimeSpan nextUpdate = TimeSpan.Zero;
    public TimeSpan updateDelay = TimeSpan.FromSeconds(15); // PlantHolder has a 15 second delay on cycles

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        if (nextUpdate > _gameTiming.CurTime)
            return;

        // Query for plant holders that have seeds and are not dead
        var query = EntityQueryEnumerator<PlantHolderComponent>();
        while (query.MoveNext(out var uid, out var plantHolder))
        {
            // Only process plants that have seeds and are alive
            if (plantHolder.Seed == null || plantHolder.Dead)
                continue;

            // Check if it's time for this plant to grow
            if (_gameTiming.CurTime < plantHolder.LastCycle + plantHolder.CycleDelay)
                continue;

            var plantGrow = new OnPlantGrowEvent();
            RaiseLocalEvent(uid, ref plantGrow);

            // Update the last cycle time after processing growth
            plantHolder.LastCycle = _gameTiming.CurTime;
        }

        nextUpdate = _gameTiming.CurTime + updateDelay;
    }
}

/// <summary>
/// Base system for plant growth mechanics. Provides common functionality for all plant growth systems.
/// </summary>
public abstract class PlantGrowthSystem : EntitySystem
{
    [Dependency] protected readonly IRobustRandom _random = default!;
    [Dependency] protected readonly IGameTiming _gameTiming = default!;

    public const float HydroponicsSpeedMultiplier = 1f;
    public const float HydroponicsConsumptionMultiplier = 2f;

    public override void Initialize()
    {
        base.Initialize();
    }

    /// <summary>
    /// Affects the growth of a plant by modifying its age or production timing.
    /// </summary>
    public void AffectGrowth(int amount, PlantHolderComponent? component = null)
    {
        if (component == null || component.Seed == null)
            return;

        if (amount > 0)
        {
            if (component.Age < component.Seed.Maturation)
                component.Age += amount;
            else if (!component.Harvest && component.Seed.Yield > 0f)
                component.LastProduce -= amount;
        }
        else
        {
            if (component.Age < component.Seed.Maturation)
                component.SkipAging++;
            else if (!component.Harvest && component.Seed.Yield > 0f)
                component.LastProduce += amount;
        }
    }
}

/// <summary>
/// Static helper class for creating default growth components.
/// </summary>
public static class DefaultGrowthComponents
{
    /// <summary>
    /// Default water consumption rate for plants.
    /// </summary>
    public const float DefaultWaterConsumption = 0.5f;

    /// <summary>
    /// Default nutrient consumption rate for plants.
    /// </summary>
    public const float DefaultNutrientConsumption = 0.5f;

    /// <summary>
    /// Default ideal temperature for plant growth in Kelvin.
    /// </summary>
    public const float DefaultIdealHeat = 298f;

    /// <summary>
    /// Default temperature tolerance range around ideal heat.
    /// </summary>
    public const float DefaultHeatTolerance = 10f;

    /// <summary>
    /// Default minimum pressure tolerance for plant growth.
    /// </summary>
    public const float DefaultLowPressureTolerance = 81f;

    /// <summary>
    /// Default maximum pressure tolerance for plant growth.
    /// </summary>
    public const float DefaultHighPressureTolerance = 121f;

    /// <summary>
    /// Creates a default BasicGrowthComponent with standard values.
    /// </summary>
    public static BasicGrowthComponent CreateDefaultBasicGrowth()
    {
        return new BasicGrowthComponent
        {
            WaterConsumption = DefaultWaterConsumption,
            NutrientConsumption = DefaultNutrientConsumption
        };
    }

    /// <summary>
    /// Creates a default AtmosphericGrowthComponent with standard values.
    /// </summary>
    public static AtmosphericGrowthComponent CreateDefaultAtmosphericGrowth()
    {
        return new AtmosphericGrowthComponent
        {
            IdealHeat = DefaultIdealHeat,
            HeatTolerance = DefaultHeatTolerance,
            LowPressureTolerance = DefaultLowPressureTolerance,
            HighPressureTolerance = DefaultHighPressureTolerance
        };
    }
}
