using Content.Server.Botany.Components;
using Content.Shared.Coordinates.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server.Botany.Systems;
public sealed class WeedPestGrowthSystem : PlantGrowthSystem
{
    public const float WeedHighLevelThreshold = 10f;
    
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WeedPestGrowthComponent, OnPlantGrowEvent>(OnPlantGrow);
        SubscribeLocalEvent<PlantHolderComponent, OnPlantGrowEvent>(OnTrayUpdate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        
        // Update all plant holders for weed growth, regardless of events
        var query = EntityQueryEnumerator<PlantHolderComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            HandleWeedGrowth(uid, component);
        }
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
        HandleWeedGrowth(uid, component);
    }

    /// <summary>
    /// Handles weed growth and kudzu transformation for all plant holders.
    /// </summary>
    private void HandleWeedGrowth(EntityUid uid, PlantHolderComponent component)
    {
        // Weeds like water and nutrients! They may appear even if there's not a seed planted. Isnt connected to the plant, stays here in PlantHolder.
        if (component.WaterLevel > 10 && component.NutritionLevel > 5)
        {
            var chance = 0f;
            if (component.Seed == null)
                chance = 0.05f;
            else if (TryComp<PlantTraitsComponent>(uid, out var traits) && traits.TurnIntoKudzu)
                chance = 1f;
            else
                chance = 0.01f;

            if (_random.Prob(chance))
                component.WeedLevel += 1 + component.WeedCoefficient;

            if (component.DrawWarnings)
                component.UpdateSpriteAfterUpdate = true;
        }

        if (component.Seed != null && TryComp<PlantTraitsComponent>(uid, out var kudzuTraits) && kudzuTraits.TurnIntoKudzu
            && component.WeedLevel >= WeedHighLevelThreshold)
        {
            Spawn(component.Seed.KudzuPrototype, Transform(uid).Coordinates.SnapToGrid(EntityManager));
            kudzuTraits.TurnIntoKudzu = false;
            component.Health = 0;
        }
    }
}
