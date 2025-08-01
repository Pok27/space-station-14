using Content.Shared.Atmos;
using Content.Shared.EntityEffects;
using Content.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.IoC;
using Robust.Shared.Serialization.Manager;
using System.Linq;

namespace Content.Server.Botany;

public sealed class MutationSystem : EntitySystem
{
    private static ProtoId<RandomPlantMutationListPrototype> RandomPlantMutations = "RandomPlantMutations";

    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    private RandomPlantMutationListPrototype _randomMutations = default!;

    public override void Initialize()
    {
        _randomMutations = _prototypeManager.Index(RandomPlantMutations);
    }

    /// <summary>
    /// For each random mutation, see if it occurs on this plant this check.
    /// </summary>
    /// <param name="seed"></param>
    /// <param name="severity"></param>
    public void CheckRandomMutations(EntityUid plantHolder, ref SeedData seed, float severity)
    {
        foreach (var mutation in _randomMutations.mutations)
        {
            if (Random(Math.Min(mutation.BaseOdds * severity, 1.0f)))
            {
                if (mutation.AppliesToPlant)
                {
                    var args = new EntityEffectBaseArgs(plantHolder, EntityManager);
                    mutation.Effect.Effect(args);
                }
                // Stat adjustments do not persist by being an attached effect, they just change the stat.
                if (mutation.Persists && !seed.Mutations.Any(m => m.Name == mutation.Name))
                    seed.Mutations.Add(mutation);
            }
        }
    }

    /// <summary>
    /// Checks all defined mutations against a seed to see which of them are applied.
    /// </summary>
    public void MutateSeed(EntityUid plantHolder, ref SeedData seed, float severity)
    {
        if (!seed.Unique)
        {
            Log.Error($"Attempted to mutate a shared seed");
            return;
        }

        CheckRandomMutations(plantHolder, ref seed, severity);
        EnsureGrowthComponents(plantHolder, seed);
    }

    /// <summary>
    /// Ensures that the plant has all the components specified in the seed data.
    /// </summary>
    private void EnsureGrowthComponents(EntityUid plantHolder, SeedData seed)
    {
        // Ensure growth components (including PlantTraitsComponent and HarvestComponent)
        foreach (var component in seed.GrowthComponents)
        {
            if (!EntityManager.HasComponent(plantHolder, component.GetType()))
            {
                var newComponent = component.DupeComponent();
                EntityManager.AddComponent(plantHolder, newComponent);
            }
        }

        // Ensure other plant components (not in growthComponents)
        if (seed.PlantCosmetics != null && !EntityManager.HasComponent<PlantCosmeticsComponent>(plantHolder))
        {
            var cosmetics = IoCManager.Resolve<ISerializationManager>().CreateCopy(seed.PlantCosmetics, notNullableOverride: true);
            EntityManager.AddComponent(plantHolder, cosmetics);
        }

        if (seed.PlantChemicals != null && !EntityManager.HasComponent<PlantChemicalsComponent>(plantHolder))
        {
            var chemicals = IoCManager.Resolve<ISerializationManager>().CreateCopy(seed.PlantChemicals, notNullableOverride: true);
            EntityManager.AddComponent(plantHolder, chemicals);
        }

        if (seed.PlantProducts != null && !EntityManager.HasComponent<PlantProductsComponent>(plantHolder))
        {
            var products = IoCManager.Resolve<ISerializationManager>().CreateCopy(seed.PlantProducts, notNullableOverride: true);
            EntityManager.AddComponent(plantHolder, products);
        }
    }

    public SeedData Cross(SeedData a, SeedData b)
    {
        SeedData result = b.Clone();

        // Cross chemicals
        if (result.PlantChemicals != null && a.PlantChemicals != null)
        {
            CrossChemicals(ref result.PlantChemicals.Chemicals, a.PlantChemicals.Chemicals);
        }

        // Cross traits and harvest from growthComponents
        var resultTraits = result.GrowthComponents.OfType<PlantTraitsComponent>().FirstOrDefault();
        var resultHarvest = result.GrowthComponents.OfType<HarvestComponent>().FirstOrDefault();
        var aTraits = a.GrowthComponents.OfType<PlantTraitsComponent>().FirstOrDefault();
        var aHarvest = a.GrowthComponents.OfType<HarvestComponent>().FirstOrDefault();

        if (resultTraits != null && aTraits != null)
        {
            CrossFloat(ref resultTraits.Endurance, aTraits.Endurance);
            CrossInt(ref resultTraits.Yield, aTraits.Yield);
            CrossFloat(ref resultTraits.Lifespan, aTraits.Lifespan);
            CrossFloat(ref resultTraits.Maturation, aTraits.Maturation);
            CrossFloat(ref resultTraits.Production, aTraits.Production);
            CrossFloat(ref resultTraits.Potency, aTraits.Potency);
            CrossBool(ref resultTraits.Seedless, aTraits.Seedless);
            CrossBool(ref resultTraits.Ligneous, aTraits.Ligneous);
            CrossBool(ref resultTraits.Viable, aTraits.Viable);
        }

        // Cross cosmetics
        if (result.PlantCosmetics != null && a.PlantCosmetics != null)
        {
            CrossBool(ref result.PlantCosmetics.TurnIntoKudzu, a.PlantCosmetics.TurnIntoKudzu);
            CrossBool(ref result.PlantCosmetics.CanScream, a.PlantCosmetics.CanScream);
        }

        // Cross harvest
        if (resultHarvest != null && aHarvest != null)
        {
            // 50% chance to use the other plant's harvest type
            if (Random(0.5f))
            {
                resultHarvest.HarvestRepeat = aHarvest.HarvestRepeat;
            }
        }

        // LINQ Explanation
        // For the list of mutation effects on both plants, use a 50% chance to pick each one.
        // Union all of the chosen mutations into one list, and pick ones with a Distinct (unique) name.
        result.Mutations = result.Mutations.Where(m => Random(0.5f)).Union(a.Mutations.Where(m => Random(0.5f))).DistinctBy(m => m.Name).ToList();

        // Hybrids have a high chance of being seedless. Balances very
        // effective hybrid crossings.
        if (a.Name != result.Name && Random(0.7f) && resultTraits != null)
        {
            resultTraits.Seedless = true;
        }

        return result;
    }

    private void CrossChemicals(ref Dictionary<string, SeedChemQuantity> val, Dictionary<string, SeedChemQuantity> other)
    {
        // Go through chemicals from the pollen in swab
        foreach (var otherChem in other)
        {
            // if both have same chemical, randomly pick potency ratio from the two.
            if (val.ContainsKey(otherChem.Key))
            {
                val[otherChem.Key] = Random(0.5f) ? otherChem.Value : val[otherChem.Key];
            }
            // if target plant doesn't have this chemical, has 50% chance to add it.
            else
            {
                if (Random(0.5f))
                {
                    var fixedChem = otherChem.Value;
                    fixedChem.Inherent = false;
                    val.Add(otherChem.Key, fixedChem);
                }
            }
        }

        // if the target plant has chemical that the pollen in swab does not, 50% chance to remove it.
        foreach (var thisChem in val)
        {
            if (!other.ContainsKey(thisChem.Key))
            {
                if (Random(0.5f))
                {
                    if (val.Count > 1)
                    {
                        val.Remove(thisChem.Key);
                    }
                }
            }
        }
    }

    private void CrossGasses(ref Dictionary<Gas, float> val, Dictionary<Gas, float> other)
    {
        // Go through gasses from the pollen in swab
        foreach (var otherGas in other)
        {
            // if both have same gas, randomly pick ammount from the two.
            if (val.ContainsKey(otherGas.Key))
            {
                val[otherGas.Key] = Random(0.5f) ? otherGas.Value : val[otherGas.Key];
            }
            // if target plant doesn't have this gas, has 50% chance to add it.
            else
            {
                if (Random(0.5f))
                {
                    val.Add(otherGas.Key, otherGas.Value);
                }
            }
        }
        // if the target plant has gas that the pollen in swab does not, 50% chance to remove it.
        foreach (var thisGas in val)
        {
            if (!other.ContainsKey(thisGas.Key))
            {
                if (Random(0.5f))
                {
                    val.Remove(thisGas.Key);
                }
            }
        }
    }
    private void CrossFloat(ref float val, float other)
    {
        val = Random(0.5f) ? val : other;
    }

    private void CrossInt(ref int val, int other)
    {
        val = Random(0.5f) ? val : other;
    }

    private void CrossBool(ref bool val, bool other)
    {
        val = Random(0.5f) ? val : other;
    }

    private bool Random(float p)
    {
        return _robustRandom.Prob(p);
    }
}
