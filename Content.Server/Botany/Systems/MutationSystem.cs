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
        // Ensure growth components
        foreach (var component in seed.GrowthComponents)
        {
            if (!EntityManager.HasComponent(plantHolder, component.GetType()))
            {
                var newComponent = component.DupeComponent();
                EntityManager.AddComponent(plantHolder, newComponent);
            }
        }

        // Ensure plant components
        if (seed.PlantTraits != null && !EntityManager.HasComponent<PlantTraitsComponent>(plantHolder))
        {
            var traits = IoCManager.Resolve<ISerializationManager>().CreateCopy(seed.PlantTraits, notNullableOverride: true);
            EntityManager.AddComponent(plantHolder, traits);
        }

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

        if (seed.Harvest != null && !EntityManager.HasComponent<HarvestComponent>(plantHolder))
        {
            var harvest = IoCManager.Resolve<ISerializationManager>().CreateCopy(seed.Harvest, notNullableOverride: true);
            EntityManager.AddComponent(plantHolder, harvest);
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

        // Cross traits
        if (result.PlantTraits != null && a.PlantTraits != null)
        {
            CrossFloat(ref result.PlantTraits.Endurance, a.PlantTraits.Endurance);
            CrossInt(ref result.PlantTraits.Yield, a.PlantTraits.Yield);
            CrossFloat(ref result.PlantTraits.Lifespan, a.PlantTraits.Lifespan);
            CrossFloat(ref result.PlantTraits.Maturation, a.PlantTraits.Maturation);
            CrossFloat(ref result.PlantTraits.Production, a.PlantTraits.Production);
            CrossFloat(ref result.PlantTraits.Potency, a.PlantTraits.Potency);
            CrossBool(ref result.PlantTraits.Seedless, a.PlantTraits.Seedless);
            CrossBool(ref result.PlantTraits.Ligneous, a.PlantTraits.Ligneous);
            CrossBool(ref result.PlantTraits.Viable, a.PlantTraits.Viable);
        }

        // Cross cosmetics
        if (result.PlantCosmetics != null && a.PlantCosmetics != null)
        {
            CrossBool(ref result.PlantCosmetics.TurnIntoKudzu, a.PlantCosmetics.TurnIntoKudzu);
            CrossBool(ref result.PlantCosmetics.CanScream, a.PlantCosmetics.CanScream);
        }

        // Cross harvest
        if (result.Harvest != null && a.Harvest != null)
        {
            // 50% chance to use the other plant's harvest type
            if (Random(0.5f))
            {
                result.Harvest.HarvestRepeat = a.Harvest.HarvestRepeat;
            }
        }

        // LINQ Explanation
        // For the list of mutation effects on both plants, use a 50% chance to pick each one.
        // Union all of the chosen mutations into one list, and pick ones with a Distinct (unique) name.
        result.Mutations = result.Mutations.Where(m => Random(0.5f)).Union(a.Mutations.Where(m => Random(0.5f))).DistinctBy(m => m.Name).ToList();

        // Hybrids have a high chance of being seedless. Balances very
        // effective hybrid crossings.
        if (a.Name != result.Name && Random(0.7f) && result.PlantTraits != null)
        {
            result.PlantTraits.Seedless = true;
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
