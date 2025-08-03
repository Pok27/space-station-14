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
        // ===== WEED GROWTH LOGIC =====
        // Weeds need water and nutrients to grow
        if (component.WaterLevel > 10 && component.NutritionLevel > 5)
        {
            // Calculate growth chance based on tray contents
            float chance;
            if (component.Seed == null)
                chance = 0.05f; // Empty trays: 5% chance
            else if (TryComp<PlantTraitsComponent>(uid, out var traits) && traits.TurnIntoKudzu)
                chance = 1f; // Kudzu mutants: 100% chance
            else
                chance = 0.01f; // Regular plants: 1% chance

            // Try to grow weeds
            if (_random.Prob(chance))
            {
                component.WeedLevel += 1 + component.WeedCoefficient;
                if (component.DrawWarnings)
                    component.UpdateSpriteAfterUpdate = true;
            }
        }
        else
        {
            // Debug: Check why weeds don't grow
            // This will help us understand if the issue is with water/nutrients
        }

        // ===== KUDZU TRANSFORMATION LOGIC =====
        // Check all conditions for kudzu transformation
        if (TryComp<WeedPestGrowthComponent>(uid, out var weed) &&
            TryComp<PlantTraitsComponent>(uid, out var kudzuTraits) &&
            kudzuTraits.TurnIntoKudzu &&
            component.WeedLevel >= weed.WeedHighLevelThreshold)
        {
            // Spawn kudzu entity
            if (component.Seed?.KudzuPrototype != null)
                Spawn(component.Seed.KudzuPrototype, Transform(uid).Coordinates.SnapToGrid(EntityManager));

            // Reset kudzu trait and kill plant
            kudzuTraits.TurnIntoKudzu = false;
            component.Health = 0;
        }
    }
}
