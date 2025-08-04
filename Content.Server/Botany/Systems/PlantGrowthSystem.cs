using Content.Server.Botany.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Collections.Generic;
using System.Linq;

namespace Content.Server.Botany.Systems;

[ByRefEvent]
public readonly record struct OnPlantGrowEvent;

/// <summary>
/// Main system that coordinates all plant growth systems.
/// Finds all plant growth systems and triggers them through events instead of individual Update() methods.
/// </summary>
public sealed class PlantSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public TimeSpan nextUpdate = TimeSpan.Zero;
    public TimeSpan updateDelay = TimeSpan.FromSeconds(15); // PlantHolder has a 15 second delay on cycles

    // Cache of all plant growth systems to avoid repeated lookups
    private List<PlantGrowthSystem> _plantGrowthSystems = new();

    public override void Initialize()
    {
        base.Initialize();

        // Find all plant growth systems and cache them
        FindPlantGrowthSystems();
    }

    /// <summary>
    /// Finds all systems that inherit from PlantGrowthSystem and caches them.
    /// This allows us to trigger growth events for all plants at once.
    /// </summary>
    private void FindPlantGrowthSystems()
    {
        _plantGrowthSystems.Clear();
        
        // Get all entity systems that inherit from PlantGrowthSystem
        var systemTypes = EntityManager.EntitySysManager.GetEntitySystemTypes();
        foreach (var systemType in systemTypes)
        {
            if (systemType.IsSubclassOf(typeof(PlantGrowthSystem)) && systemType != typeof(PlantGrowthSystem))
            {
                var system = EntityManager.EntitySysManager.GetEntitySystem(systemType) as PlantGrowthSystem;
                if (system != null)
                {
                    _plantGrowthSystems.Add(system);
                }
            }
        }
    }

    public override void Update(float frameTime)
    {
        if (nextUpdate > _gameTiming.CurTime)
            return;

        // Trigger growth event for all plants with PlantComponent
        var query = EntityQueryEnumerator<PlantComponent>();
        while (query.MoveNext(out var uid, out var plantComponent))
        {
            var plantGrow = new OnPlantGrowEvent();
            RaiseLocalEvent(uid, ref plantGrow);
        }

        nextUpdate = _gameTiming.CurTime + updateDelay;
    }
}

/// <summary>
/// Base system for plant growth mechanics. Provides common functionality for all plant growth systems.
/// Individual growth systems should inherit from this and listen for OnPlantGrowEvent.
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
        
        // Subscribe to plant growth events instead of using individual Update() methods
        SubscribeLocalEvent<PlantComponent, OnPlantGrowEvent>(OnPlantGrow);
    }

    /// <summary>
    /// Called when a plant growth event is triggered.
    /// Override this in derived systems to handle specific growth logic.
    /// </summary>
    protected virtual void OnPlantGrow(EntityUid uid, PlantComponent component, ref OnPlantGrowEvent args)
    {
        // Base implementation - can be overridden by derived systems
    }

    /// <summary>
    /// Affects the growth of a plant by modifying its age or production timing.
    /// </summary>
    public void AffectGrowth(EntityUid uid, int amount, PlantHolderComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.Seed == null)
            return;

        PlantTraitsComponent? traits = null;
        Resolve<PlantTraitsComponent>(uid, ref traits);

        if (traits == null)
            return;

        // Synchronize harvest status with HarvestComponent if present
        if (TryComp<HarvestComponent>(uid, out var harvestComp))
        {
            component.Harvest = harvestComp.ReadyForHarvest;
        }

        if (amount > 0)
        {
            if (component.Age < traits.Maturation)
                component.Age += amount;
            else if (!component.Harvest && traits.Yield > 0f)
                component.LastProduce -= amount;
        }
        else
        {
            if (component.Age < traits.Maturation)
                component.SkipAging++;
            else if (!component.Harvest && traits.Yield > 0f)
                component.LastProduce += amount;
        }
    }
}
