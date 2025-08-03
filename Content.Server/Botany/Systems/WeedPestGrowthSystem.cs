using Content.Server.Botany.Components;
using Content.Shared.Coordinates.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server.Botany.Systems;
public sealed class WeedPestGrowthSystem : PlantGrowthSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WeedPestGrowthComponent, OnPlantGrowEvent>(OnPlantGrow);
        SubscribeLocalEvent<PlantHolderComponent, OnPlantGrowEvent>(OnTrayUpdate);
    }

    private void OnPlantGrow(EntityUid uid, WeedPestGrowthComponent component, OnPlantGrowEvent args)
    {
        PlantHolderComponent? holder = null;
        Resolve<PlantHolderComponent>(uid, ref holder);

        if (holder == null || holder.Seed == null || holder.Dead)
            return;

        // Weed growth logic
        if (_random.Prob(component.WeedGrowthChance))
        {
            holder.WeedLevel += component.WeedGrowthAmount;
            if (holder.DrawWarnings)
                holder.UpdateSpriteAfterUpdate = true;
        }

        // Pest damage logic
        if (_random.Prob(component.PestDamageChance))
        {
            holder.Health -= component.PestDamageAmount;
            if (holder.DrawWarnings)
                holder.UpdateSpriteAfterUpdate = true;
        }
    }

    /// <summary>
    /// Handles weed growth and kudzu transformation for plant holder trays.
    /// </summary>
    private void OnTrayUpdate(EntityUid uid, PlantHolderComponent component, OnPlantGrowEvent args)
    {
        // Handle weed growth
        HandleWeedGrowth(uid, component);

        // Handle kudzu transformation
        HandleKudzuTransformation(uid, component);
    }

    /// <summary>
    /// Handles weed growth in plant holder trays.
    /// </summary>
    private void HandleWeedGrowth(EntityUid uid, PlantHolderComponent component)
    {
        // Weeds need water and nutrients to grow
        if (component.WaterLevel <= 10 || component.NutritionLevel <= 5)
            return;

        // Calculate weed growth chance based on tray state
        var chance = CalculateWeedGrowthChance(uid, component);

        // Try to grow weeds
        if (_random.Prob(chance))
        {
            component.WeedLevel += 1 + component.WeedCoefficient;
            
            if (component.DrawWarnings)
                component.UpdateSpriteAfterUpdate = true;
        }
    }

    /// <summary>
    /// Calculates weed growth chance based on tray contents.
    /// </summary>
    private float CalculateWeedGrowthChance(EntityUid uid, PlantHolderComponent component)
    {
        // Empty trays have higher weed growth chance
        if (component.Seed == null)
            return 0.05f;

        // Kudzu mutants have guaranteed weed growth
        if (TryComp<PlantTraitsComponent>(uid, out var traits) && traits.TurnIntoKudzu)
            return 1f;

        // Regular plants have low weed growth chance
        return 0.01f;
    }

    /// <summary>
    /// Handles kudzu transformation for plants with TurnIntoKudzu trait.
    /// </summary>
    private void HandleKudzuTransformation(EntityUid uid, PlantHolderComponent component)
    {
        // Check if plant has WeedPestGrowthComponent
        if (!TryComp<WeedPestGrowthComponent>(uid, out var weed))
            return;

        // Check if plant has kudzu trait
        if (!TryComp<PlantTraitsComponent>(uid, out var traits) || !traits.TurnIntoKudzu)
            return;

        // Check if weed level is high enough for transformation
        if (component.WeedLevel < weed.WeedHighLevelThreshold)
            return;

        // Transform to kudzu
        if (component.Seed?.KudzuPrototype != null)
        {
            Spawn(component.Seed.KudzuPrototype, Transform(uid).Coordinates.SnapToGrid(EntityManager));
        }

        // Reset kudzu trait and kill plant
        traits.TurnIntoKudzu = false;
        component.Health = 0;
    }
}
