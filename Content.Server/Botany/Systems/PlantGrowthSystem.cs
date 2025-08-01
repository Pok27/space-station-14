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

        var query = EntityQueryEnumerator<PlantHolderComponent>();
        while (query.MoveNext(out var uid, out var plantHolder))
        {
            if (plantHolder.Seed == null || plantHolder.Dead)
                continue;

            if (_gameTiming.CurTime < plantHolder.LastCycle + plantHolder.CycleDelay)
                continue;

            var plantGrow = new OnPlantGrowEvent();
            RaiseLocalEvent(uid, ref plantGrow);

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

    /// <summary>
    /// Multiplier for plant growth speed in hydroponics.
    /// </summary>
    public const float HydroponicsSpeedMultiplier = 1f;

    /// <summary>
    /// Multiplier for resource consumption (water, nutrients) in hydroponics.
    /// </summary>
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

    /// <summary>
    /// Creates default growth components for seeds that don't have them yet.
    /// This is used for backward compatibility with old seeds that have parameters directly in SeedData.
    /// </summary>
    public void EnsureDefaultGrowthComponents(EntityUid uid, SeedData seed)
    {
        // Always ensure basic components
        EnsureComp<PlantComponent>(uid);
        EnsureComp<BasicGrowthComponent>(uid);
        EnsureComp<AtmosphericGrowthComponent>(uid);
        EnsureComp<WeedPestGrowthComponent>(uid);

        // Check if seed has growth components defined
        if (seed.GrowthComponents.Count > 0)
            return;

        // Create PlantTraitsComponent from seed parameters
        var traits = EnsureComp<PlantTraitsComponent>(uid);
        
        // Set default values if not already set
        if (traits.Lifespan == 0)
            traits.Lifespan = 25;
        if (traits.Maturation == 0)
            traits.Maturation = 6;
        if (traits.Production == 0)
            traits.Production = 6;
        if (traits.Yield == 0)
            traits.Yield = 3;
        if (traits.Potency == 1f)
            traits.Potency = 10;
        if (traits.GrowthStages == 6)
            traits.GrowthStages = 3;

        // Create HarvestComponent if needed
        if (seed.HarvestRepeat != HarvestType.NoRepeat)
        {
            var harvest = EnsureComp<HarvestComponent>(uid);
            harvest.HarvestRepeat = seed.HarvestRepeat;
        }

        // Create PlantProductsComponent
        var products = EnsureComp<PlantProductsComponent>(uid);
        products.ProductPrototypes.Clear();
        products.ProductPrototypes.AddRange(seed.ProductPrototypes);

        // Create PlantCosmeticsComponent
        var cosmetics = EnsureComp<PlantCosmeticsComponent>(uid);
        cosmetics.CanScream = seed.CanScream;
        cosmetics.ScreamSound = seed.ScreamSound;
        cosmetics.TurnIntoKudzu = seed.TurnIntoKudzu;
        cosmetics.KudzuPrototype = seed.KudzuPrototype;
    }
}
