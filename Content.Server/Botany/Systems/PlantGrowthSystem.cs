using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Robust.Server.GameObjects;
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
    [Dependency] private readonly MutationSystem _mutation = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<PlantHolderComponent>();
        while (query.MoveNext(out var uid, out var plantHolder))
        {
            // Check for growth cycle using CycleDelay
            if (_gameTiming.CurTime >= (plantHolder.LastCycle + plantHolder.CycleDelay))
            {
                ProcessGrowthCycle(uid, plantHolder);
            }
        }
    }

    private void ProcessGrowthCycle(EntityUid uid, PlantHolderComponent component)
    {
        var curTime = _gameTiming.CurTime;

        // ForceUpdate is used for external triggers like swabbing
        if (component.ForceUpdate)
            component.ForceUpdate = false;
        else
        {
            // This method is only called when CycleDelay has passed
            component.LastCycle = curTime;
        }

        if (component.Seed != null && !component.Dead)
        {
            var plantGrow = new OnPlantGrowEvent();
            RaiseLocalEvent(uid, ref plantGrow);
        }

        // Process mutations. All plants can mutate, so this stays here.
        if (component.MutationLevel > 0)
        {
            Mutate(uid, Math.Min(component.MutationLevel, 25), component);
            component.UpdateSpriteAfterUpdate = true;
            component.MutationLevel = 0;
        }

        // If we have no seed planted, or the plant is dead, stop processing here.
        if (component.Seed == null || component.Dead)
        {
            if (component.UpdateSpriteAfterUpdate)
                UpdateSprite(uid, component);

            return;
        }

        CheckHealth(uid, component);
        CheckLevelSanity(uid, component);

        // Synchronize harvest status between PlantHolderComponent and HarvestComponent
        if (TryComp<HarvestComponent>(uid, out var harvestComp))
        {
            component.Harvest = harvestComp.ReadyForHarvest;
        }

        if (component.UpdateSpriteAfterUpdate)
            UpdateSprite(uid, component);
    }

    private void Mutate(EntityUid uid, float severity, PlantHolderComponent component)
    {
        if (component.Seed != null)
        {
            EnsureUniqueSeed(uid, component);
            _mutation.MutateSeed(uid, ref component.Seed, severity);
        }
    }

    private void EnsureUniqueSeed(EntityUid uid, PlantHolderComponent component)
    {
        if (component.Seed is { Unique: false })
            component.Seed = component.Seed.Clone();
    }

    private void CheckHealth(EntityUid uid, PlantHolderComponent component)
    {
        if (component.Health <= 0)
        {
            Die(uid, component);
        }
    }

    private void Die(EntityUid uid, PlantHolderComponent component)
    {
        component.Dead = true;
        component.Harvest = false;
        component.MutationLevel = 0;
        component.YieldMod = 1;
        component.MutationMod = 1;
        component.ImproperPressure = false;
        component.WeedLevel += 1;
        component.PestLevel = 0;
        UpdateSprite(uid, component);
    }

    private void CheckLevelSanity(EntityUid uid, PlantHolderComponent component)
    {
        if (component.Seed != null && TryComp<PlantTraitsComponent>(uid, out var traits))
            component.Health = MathHelper.Clamp(component.Health, 0, traits.Endurance);
        else
        {
            component.Health = 0f;
            component.Dead = false;
        }

        component.MutationLevel = MathHelper.Clamp(component.MutationLevel, 0f, 100f);
        component.NutritionLevel = MathHelper.Clamp(component.NutritionLevel, 0f, 100f);
        component.WaterLevel = MathHelper.Clamp(component.WaterLevel, 0f, 100f);
        component.PestLevel = MathHelper.Clamp(component.PestLevel, 0f, 10f);
        component.WeedLevel = MathHelper.Clamp(component.WeedLevel, 0f, 10f);
        component.Toxins = MathHelper.Clamp(component.Toxins, 0f, 100f);
        component.YieldMod = MathHelper.Clamp(component.YieldMod, 0, 2);
        component.MutationMod = MathHelper.Clamp(component.MutationMod, 0f, 3f);
    }

    private void UpdateSprite(EntityUid uid, PlantHolderComponent component)
    {
        component.UpdateSpriteAfterUpdate = false;

        if (!TryComp<AppearanceComponent>(uid, out var app))
            return;

        TryComp<PlantTraitsComponent>(uid, out var spriteTraits);

        if (component.Seed != null)
        {
            if (component.DrawWarnings)
            {
                _appearance.SetData(uid, PlantHolderVisuals.HealthLight, component.Health <= (spriteTraits?.Endurance ?? 100f) / 2f);
            }

            if (component.Dead)
            {
                _appearance.SetData(uid, PlantHolderVisuals.PlantRsi, component.Seed.PlantRsi.ToString(), app);
                _appearance.SetData(uid, PlantHolderVisuals.PlantState, "dead", app);
            }
            else if (component.Harvest)
            {
                _appearance.SetData(uid, PlantHolderVisuals.PlantRsi, component.Seed.PlantRsi.ToString(), app);
                _appearance.SetData(uid, PlantHolderVisuals.PlantState, "harvest", app);
            }
            else
            {
                if (spriteTraits == null)
                    return;

                if (component.Age < spriteTraits.Maturation)
                {
                    var growthStage = GetCurrentGrowthStage(uid, component);

                    _appearance.SetData(uid, PlantHolderVisuals.PlantRsi, component.Seed.PlantRsi.ToString(), app);
                    _appearance.SetData(uid, PlantHolderVisuals.PlantState, $"stage-{growthStage}", app);
                    component.LastProduce = component.Age;
                }
                else
                {
                    _appearance.SetData(uid, PlantHolderVisuals.PlantRsi, component.Seed.PlantRsi.ToString(), app);
                    _appearance.SetData(uid, PlantHolderVisuals.PlantState, $"stage-{spriteTraits.GrowthStages}", app);
                }
            }
        }
        else
        {
            _appearance.SetData(uid, PlantHolderVisuals.PlantState, "", app);
            _appearance.SetData(uid, PlantHolderVisuals.HealthLight, false, app);
        }

        if (!component.DrawWarnings)
            return;

        _appearance.SetData(uid, PlantHolderVisuals.WaterLight, component.WaterLevel <= 15, app);
        _appearance.SetData(uid, PlantHolderVisuals.NutritionLight, component.NutritionLevel <= 8, app);
        _appearance.SetData(uid, PlantHolderVisuals.AlertLight,
            component.WeedLevel >= 5 || component.PestLevel >= 5 || component.Toxins >= 40 || component.ImproperHeat ||
            component.ImproperPressure || component.MissingGas > 0, app);
        _appearance.SetData(uid, PlantHolderVisuals.HarvestLight, component.Harvest, app);
    }

    private int GetCurrentGrowthStage(EntityUid uid, PlantHolderComponent component)
    {
        if (component.Seed == null)
            return 0;

        if (!TryComp<PlantTraitsComponent>(uid, out var traits))
            return 1;

        var result = Math.Max(1, (int)(component.Age * traits.GrowthStages / traits.Maturation));
        return result;
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
