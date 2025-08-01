using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Server.EntityEffects;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.IoC;

namespace Content.Server.Botany;

[Prototype]
public sealed partial class SeedPrototype : SeedData, IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
}

public enum HarvestType : byte
{
    NoRepeat,
    Repeat,
    SelfHarvest
}

[DataDefinition]
public partial struct SeedChemQuantity
{
    /// <summary>
    /// Minimum amount of chemical that is added to produce, regardless of the potency
    /// </summary>
    [DataField("Min")] public int Min;

    /// <summary>
    /// Maximum amount of chemical that can be produced after taking plant potency into account.
    /// </summary>
    [DataField("Max")] public int Max;

    /// <summary>
    /// When chemicals are added to produce, the potency of the seed is divided with this value. Final chemical amount is the result plus the `Min` value.
    /// Example: PotencyDivisor of 20 with seed potency of 55 results in 2.75, 55/20 = 2.75. If minimum is 1 then final result will be 3.75 of that chemical, 55/20+1 = 3.75.
    /// </summary>
    [DataField("PotencyDivisor")] public int PotencyDivisor;

    /// <summary>
    /// Inherent chemical is one that is NOT result of mutation or crossbreeding. These chemicals are removed if species mutation is executed.
    /// </summary>
    [DataField("Inherent")] public bool Inherent = true;
}

// TODO reduce the number of friends to a reasonable level. Requires ECS-ing things like plant holder component.
[Virtual, DataDefinition]
[Access(typeof(BotanySystem), typeof(PlantHolderSystem), typeof(SeedExtractorSystem), typeof(PlantHolderComponent), typeof(EntityEffectSystem), typeof(MutationSystem))]
public partial class SeedData
{
    #region Tracking

    /// <summary>
    /// The name of this seed. Determines the name of seed packets.
    /// </summary>
    [DataField]
    public string Name { get; private set; } = "";

    /// <summary>
    /// The noun for this type of seeds. E.g. for fungi this should probably be "spores" instead of "seeds". Also
    /// used to determine the name of seed packets.
    /// </summary>
    [DataField]
    public string Noun { get; private set; } = "";

    /// <summary>
    /// Name displayed when examining the hydroponics tray. Describes the actual plant, not the seed itself.
    /// </summary>
    [DataField]
    public string DisplayName { get; private set; } = "";

    [DataField] public bool Mysterious;

    /// <summary>
    /// If true, the properties of this seed cannot be modified.
    /// </summary>
    [DataField] public bool Immutable;

    /// <summary>
    /// If true, there is only a single reference to this seed and it's properties can be directly modified without
    /// needing to clone the seed.
    /// </summary>
    [ViewVariables]
    public bool Unique = false; // seed-prototypes or yaml-defined seeds for entity prototypes will not generally be unique.
    #endregion

    #region Plant Components
    /// <summary>
    /// The plant traits component that will be applied to the plant.
    /// </summary>
    [DataField]
    public PlantTraitsComponent? PlantTraits;

    /// <summary>
    /// The plant cosmetics component that will be applied to the plant.
    /// </summary>
    [DataField]
    public PlantCosmeticsComponent? PlantCosmetics;

    /// <summary>
    /// The plant chemicals component that will be applied to the plant.
    /// </summary>
    [DataField]
    public PlantChemicalsComponent? PlantChemicals;

    /// <summary>
    /// The plant products component that will be applied to the plant.
    /// </summary>
    [DataField]
    public PlantProductsComponent? PlantProducts;

    /// <summary>
    /// The harvest component that will be applied to the plant.
    /// </summary>
    [DataField]
    public HarvestComponent? Harvest;

    #endregion

    /// <summary>
    /// The mutation effects that have been applied to this plant.
    /// </summary>
    [DataField] public List<RandomPlantMutation> Mutations { get; set; } = new();

    /// <summary>
    /// The seed prototypes this seed may mutate into when prompted to.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdListSerializer<SeedPrototype>))]
    public List<string> MutationPrototypes = new();

    /// <summary>
    /// The growth components used by this seed.
    /// </summary>
    [DataField]
    public List<PlantGrowthComponent> GrowthComponents = new();

    /// <summary>
    /// If false, rapidly decrease health while growing. Adds a bit of challenge to keep mutated plants alive via Unviable's frequency.
    /// </summary>
    [DataField]
    public bool Viable = true;

    /// <summary>
    /// Log impact for harvest operations.
    /// </summary>
    [DataField]
    public LogImpact? HarvestLogImpact;

    /// <summary>
    /// Log impact for plant operations.
    /// </summary>
    [DataField]
    public LogImpact? PlantLogImpact;

    public SeedData Clone()
    {
        DebugTools.Assert(!Immutable, "There should be no need to clone an immutable seed.");

        var newSeed = new SeedData
        {
            GrowthComponents = new List<PlantGrowthComponent>(),
            HarvestLogImpact = HarvestLogImpact,
            PlantLogImpact = PlantLogImpact,
            Name = Name,
            Noun = Noun,
            DisplayName = DisplayName,
            Mysterious = Mysterious,
            MutationPrototypes = new List<string>(MutationPrototypes),
            Mutations = new List<RandomPlantMutation>(),

            // Clone plant components
            PlantTraits = PlantTraits != null ? IoCManager.Resolve<ISerializationManager>().CreateCopy(PlantTraits, notNullableOverride: true) : null,
            PlantCosmetics = PlantCosmetics != null ? IoCManager.Resolve<ISerializationManager>().CreateCopy(PlantCosmetics, notNullableOverride: true) : null,
            PlantChemicals = PlantChemicals != null ? IoCManager.Resolve<ISerializationManager>().CreateCopy(PlantChemicals, notNullableOverride: true) : null,
            PlantProducts = PlantProducts != null ? IoCManager.Resolve<ISerializationManager>().CreateCopy(PlantProducts, notNullableOverride: true) : null,
            Harvest = Harvest != null ? IoCManager.Resolve<ISerializationManager>().CreateCopy(Harvest, notNullableOverride: true) : null,

            // Newly cloned seed is unique. No need to unnecessarily clone if repeatedly modified.
            Unique = true,
        };

        // Deep copy growth components
        foreach (var component in GrowthComponents)
        {
            var newComponent = component.DupeComponent();
            newSeed.GrowthComponents.Add(newComponent);
        }

        newSeed.Mutations.AddRange(Mutations);
        return newSeed;
    }


    /// <summary>
    /// Handles copying most species defining data from 'other' to this seed while keeping the accumulated mutations intact.
    /// </summary>
    public SeedData SpeciesChange(SeedData other)
    {
        var newSeed = new SeedData
        {
            GrowthComponents = new List<PlantGrowthComponent>(),
            HarvestLogImpact = other.HarvestLogImpact,
            PlantLogImpact = other.PlantLogImpact,
            Name = other.Name,
            Noun = other.Noun,
            DisplayName = other.DisplayName,
            Mysterious = other.Mysterious,
            MutationPrototypes = new List<string>(other.MutationPrototypes),
            Mutations = Mutations,

            // Copy plant components from the new species
            PlantTraits = other.PlantTraits != null ? IoCManager.Resolve<ISerializationManager>().CreateCopy(other.PlantTraits, notNullableOverride: true) : null,
            PlantCosmetics = other.PlantCosmetics != null ? IoCManager.Resolve<ISerializationManager>().CreateCopy(other.PlantCosmetics, notNullableOverride: true) : null,
            PlantProducts = other.PlantProducts != null ? IoCManager.Resolve<ISerializationManager>().CreateCopy(other.PlantProducts, notNullableOverride: true) : null,
            Harvest = other.Harvest != null ? IoCManager.Resolve<ISerializationManager>().CreateCopy(other.Harvest, notNullableOverride: true) : null,

            // Merge chemicals from both species
            PlantChemicals = MergeChemicals(other),

            // Newly cloned seed is unique. No need to unnecessarily clone if repeatedly modified.
            Unique = true,
        };

        // Deep copy growth components from the new species
        foreach (var component in other.GrowthComponents)
        {
            var newComponent = component.DupeComponent();
            newSeed.GrowthComponents.Add(newComponent);
        }

        return newSeed;
    }

    /// <summary>
    /// Merges chemicals from the current seed and the new species, preserving mutations.
    /// </summary>
    private PlantChemicalsComponent? MergeChemicals(SeedData other)
    {
        if (PlantChemicals == null && other.PlantChemicals == null)
            return null;

        var mergedChemicals = new PlantChemicalsComponent();
        
        // Start with current chemicals
        if (PlantChemicals != null)
        {
            foreach (var chem in PlantChemicals.Chemicals)
            {
                mergedChemicals.Chemicals[chem.Key] = chem.Value;
            }
        }

        // Add new chemicals from the other species
        if (other.PlantChemicals != null)
        {
            foreach (var otherChem in other.PlantChemicals.Chemicals)
            {
                mergedChemicals.Chemicals.TryAdd(otherChem.Key, otherChem.Value);
            }
        }

        // Remove inherent chemicals that are no longer in the new species
        if (other.PlantChemicals != null)
        {
            var chemicalsToRemove = new List<string>();
            foreach (var chem in mergedChemicals.Chemicals)
            {
                if (!other.PlantChemicals.Chemicals.ContainsKey(chem.Key) && chem.Value.Inherent)
                {
                    chemicalsToRemove.Add(chem.Key);
                }
            }
            foreach (var chemKey in chemicalsToRemove)
            {
                mergedChemicals.Chemicals.Remove(chemKey);
            }
        }

        return mergedChemicals;
    }
}
