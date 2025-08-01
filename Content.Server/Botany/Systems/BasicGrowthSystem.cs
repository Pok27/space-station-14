using Content.Server.Botany.Components;
using Content.Shared.Swab;
using Robust.Shared.Random;

namespace Content.Server.Botany.Systems;
// For all the very common stuff all plants are expected to do.

// TODO: make CO2Boost (add potency if the plant can eat an increasing amount of CO2). separate PR post-merge
// TODO: make GrowLight (run bonus ticks if theres a grow light nearby). separate PR post-merge.
public sealed class BasicGrowthSystem : PlantGrowthSystem
{
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolder = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BasicGrowthComponent, OnPlantGrowEvent>(OnPlantGrow);
        SubscribeLocalEvent<BasicGrowthComponent, BotanySwabDoAfterEvent>(OnSwab);
    }

    private void OnSwab(EntityUid uid, BasicGrowthComponent component, BotanySwabDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !TryComp<PlantHolderComponent>(args.Args.Target, out var plant) ||
            args.Used == null || !TryComp<BotanySwabComponent>(args.Used.Value, out var swab))
            return;

        var swabComp = swab.components.Find(c => c.GetType() == typeof(BasicGrowthComponent));
        if (swabComp == null)
        {
            swab.components.Add(new BasicGrowthComponent() {
                WaterConsumption = component.WaterConsumption,
                NutrientConsumption = component.NutrientConsumption
            });
        }
        else
        {
            BasicGrowthComponent typedComp = (BasicGrowthComponent)swabComp;
            if (_random.Prob(0.5f)) typedComp.WaterConsumption = component.WaterConsumption;
            if (_random.Prob(0.5f)) typedComp.NutrientConsumption = component.NutrientConsumption;
        }
    }

    private void OnPlantGrow(EntityUid uid, BasicGrowthComponent component, OnPlantGrowEvent args)
    {
        PlantHolderComponent? holder = null;
        Resolve<PlantHolderComponent>(uid, ref holder);

        if (holder == null || holder.Seed == null || holder.Dead)
            return;

        // Check if the plant is viable
        var viable = true;
        if (TryComp<PlantTraitsComponent>(uid, out var traits))
        {
            viable = traits.Viable;
        }
        else if (holder.Seed != null)
        {
            // Fallback to old system
            viable = holder.Seed.Viable;
        }

        if (!viable)
        {
            holder.Health -= _random.Next(5, 10) * PlantGrowthSystem.HydroponicsSpeedMultiplier;
            if (holder.DrawWarnings)
                holder.UpdateSpriteAfterUpdate = true;
            return;
        }

        // Advance plant age here.
        if (holder.SkipAging > 0)
            holder.SkipAging--;
        else
        {
            if (_random.Prob(0.8f))
            {
                holder.Age += (int)(1 * PlantGrowthSystem.HydroponicsSpeedMultiplier);
                holder.UpdateSpriteAfterUpdate = true;
            }
        }

        var lifespan = 0f;
        var production = 0f;
        var hasProducts = false;

        if (TryComp<PlantTraitsComponent>(uid, out var plantTraits))
        {
            lifespan = plantTraits.Lifespan;
            production = plantTraits.Production;
        }
        else if (holder.Seed != null)
        {
            // Fallback to old system
            lifespan = holder.Seed.Lifespan;
            production = holder.Seed.Production;
        }

        if (TryComp<PlantProductsComponent>(uid, out var products))
        {
            hasProducts = products.ProductPrototypes.Count > 0;
        }
        else if (holder.Seed != null)
        {
            // Fallback to old system
            hasProducts = holder.Seed.ProductPrototypes.Count > 0;
        }

        if (holder.Age > lifespan)
        {
            holder.Health -= _random.Next(3, 5) * PlantGrowthSystem.HydroponicsSpeedMultiplier;
            if (holder.DrawWarnings)
                holder.UpdateSpriteAfterUpdate = true;
        }
        else if (holder.Age < 0) // Revert back to seed packet!
        {
            var packetSeed = holder.Seed;
            // will put it in the trays hands if it has any, please do not try doing this
            _botany.SpawnSeedPacket(packetSeed, Transform(uid).Coordinates, uid);
            _plantHolder.RemovePlant(uid, holder);
            holder.ForceUpdate = true;
            _plantHolder.Update(uid, holder);
        }

        // If enough time has passed since the plant was harvested, we're ready to harvest again!
        if (hasProducts)
        {
            if (holder.Age > production)
            {
                if (holder.Age - holder.LastProduce > production && !holder.Harvest)
                {
                    holder.Harvest = true;
                    holder.LastProduce = holder.Age;
                }
            }
            else
            {
                if (holder.Harvest)
                {
                    holder.Harvest = false;
                    holder.LastProduce = holder.Age;
                }
            }
        }

        if (component.WaterConsumption > 0 && holder.WaterLevel > 0 && _random.Prob(0.75f))
        {
            holder.WaterLevel -= MathF.Max(0f,
                component.WaterConsumption * PlantGrowthSystem.HydroponicsConsumptionMultiplier * PlantGrowthSystem.HydroponicsSpeedMultiplier);
            if (holder.DrawWarnings)
                holder.UpdateSpriteAfterUpdate = true;
        }

        if (component.NutrientConsumption > 0 && holder.NutritionLevel > 0 && _random.Prob(0.75f))
        {
            holder.NutritionLevel -= MathF.Max(0f,
                component.NutrientConsumption * PlantGrowthSystem.HydroponicsConsumptionMultiplier * PlantGrowthSystem.HydroponicsSpeedMultiplier);
            if (holder.DrawWarnings)
                holder.UpdateSpriteAfterUpdate = true;
        }

        var healthMod = _random.Next(1, 3) * PlantGrowthSystem.HydroponicsSpeedMultiplier;
        if (holder.SkipAging < 10)
        {
            // Make sure the plant is not thirsty.
            if (holder.WaterLevel > 10)
            {
                holder.Health += Convert.ToInt32(_random.Prob(0.35f)) * healthMod;
            }
            else
            {
                AffectGrowth(-1, holder);
                holder.Health -= healthMod;
            }

            if (holder.NutritionLevel > 5)
            {
                holder.Health += Convert.ToInt32(_random.Prob(0.35f)) * healthMod;
            }
            else
            {
                AffectGrowth(-1, holder);
                holder.Health -= healthMod;
            }
        }
    }
}
