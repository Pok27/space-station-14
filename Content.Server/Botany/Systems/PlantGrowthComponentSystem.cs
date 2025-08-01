using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;

namespace Content.Server.Botany.Systems;

/// <summary>
/// System that ensures all plants have the necessary growth components.
/// For old seeds that don't have growthComponents defined, it creates default components based on their legacy parameters.
/// </summary>
public sealed class PlantGrowthComponentSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantHolderComponent, ComponentStartup>(OnPlantHolderStartup);
    }

    private void OnPlantHolderStartup(EntityUid uid, PlantHolderComponent component, ComponentStartup args)
    {
        if (component.Seed == null)
            return;

        // If the seed already has growth components defined, we don't need to create defaults
        if (component.Seed.GrowthComponents.Count > 0)
            return;

        // Create default components based on the seed's legacy parameters
        CreateDefaultGrowthComponents(uid, component.Seed);
    }

    private void CreateDefaultGrowthComponents(EntityUid uid, SeedData seed)
    {
        // Always ensure basic components
        EnsureComp<PlantComponent>(uid);
        EnsureComp<BasicGrowthComponent>(uid);
        EnsureComp<AtmosphericGrowthComponent>(uid);
        EnsureComp<WeedPestGrowthComponent>(uid);

        // Create PlantTraitsComponent from seed parameters
        var traits = EnsureComp<PlantTraitsComponent>(uid);
        traits.Lifespan = seed.Lifespan;
        traits.Maturation = seed.Maturation;
        traits.Production = seed.Production;
        traits.Yield = seed.Yield;
        traits.Potency = seed.Potency;
        traits.GrowthStages = seed.GrowthStages;

        // Create HarvestComponent if needed
        if (seed.HarvestRepeat != HarvestType.NoRepeat)
        {
            var harvest = EnsureComp<HarvestComponent>(uid);
            harvest.HarvestRepeat = seed.HarvestRepeat;
        }
    }
}