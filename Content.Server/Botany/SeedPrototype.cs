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

    #region Output
    /// <summary>
    /// The entity prototype that is spawned when this type of seed is extracted from produce using a seed extractor.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string PacketPrototype = "SeedBase";

    /// <summary>
    /// The entity prototypes that are spawned when this type of seed is harvested.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
    public List<string> ProductPrototypes = new();

    [DataField] public Dictionary<string, SeedChemQuantity> Chemicals = new();

    #endregion

    #region General traits

    /// <summary>
    /// The plant's max health.
    /// </summary>
    [DataField] public float Endurance = 100f;

    /// <summary>
    /// How many produce are created on harvest.
    /// </summary>
    [DataField] public int Yield;

    /// <summary>
    /// The number of growth ticks this plant can be alive for. Plants take high damage levels when Age > Lifespan.
    /// </summary>
    [DataField] public float Lifespan;

    /// <summary>
    /// The number of growth ticks it takes for a plant to reach its final growth stage.
    /// </summary>
    [DataField] public float Maturation;

    /// <summary>
    /// The number of growth ticks it takes for a plant to be (re-)harvestable. Shouldn't be lower than Maturation.
    /// </summary>
    [DataField] public float Production;

    /// <summary>
    /// How many different sprites appear before the plant is fully grown.
    /// </summary>
    [DataField] public int GrowthStages = 6;

    /// <summary>
    /// Harvest options are NoRepeat(plant is removed on harvest), Repeat(Plant makes produce every Production ticks),
    /// and SelfHarvest (Repeat, plus produce is dropped on the ground near the plant automatically)
    /// </summary>
    [DataField] public HarvestType HarvestRepeat = HarvestType.NoRepeat;

    /// <summary>
    /// A scalar for sprite size and chemical quantity on the produce. Caps at 100.
    /// </summary>
    [DataField] public float Potency = 1f;

    /// <summary>
    /// If true, produce can't be put into the seed maker.
    /// </summary>
    [DataField] public bool Seedless = false;

    /// <summary>
    /// If true, a sharp tool is required to harvest this plant.
    /// </summary>
    [DataField] public bool Ligneous;

    #endregion

    #region Cosmetics

    [DataField(required: true)]
    public ResPath PlantRsi { get; set; } = default!;

    [DataField] public string PlantIconState { get; set; } = "produce";

    /// <summary>
    /// Screams random sound from collection SoundCollectionSpecifier
    /// </summary>
    [DataField]
    public SoundSpecifier ScreamSound = new SoundCollectionSpecifier("PlantScreams", AudioParams.Default.WithVolume(-10));

    /// <summary>
    /// If true, AAAAAAAAAAAHHHHHHHHHHH!
    /// </summary>
    [DataField("screaming")] public bool CanScream;

    /// <summary>
    /// Which kind of kudzu this plant will turn into if it kuzuifies.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))] public string KudzuPrototype = "WeakKudzu";

    /// <summary>
    /// If true, this plant turns into it's KudzuPrototype when the PlantHolder's WeedLevel hits this plant's WeedHighLevelThreshold.
    /// </summary>
    [DataField] public bool TurnIntoKudzu;

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
            Viable = Viable,
            HarvestLogImpact = HarvestLogImpact,
            PlantLogImpact = PlantLogImpact,
            Name = Name,
            Noun = Noun,
            DisplayName = DisplayName,
            Mysterious = Mysterious,

            PacketPrototype = PacketPrototype,
            ProductPrototypes = new List<string>(ProductPrototypes),
            MutationPrototypes = new List<string>(MutationPrototypes),
            Chemicals = new Dictionary<string, SeedChemQuantity>(Chemicals),

            Endurance = Endurance,
            Yield = Yield,
            Lifespan = Lifespan,
            Maturation = Maturation,
            Production = Production,
            GrowthStages = GrowthStages,
            HarvestRepeat = HarvestRepeat,
            Potency = Potency,

            Seedless = Seedless,
            Ligneous = Ligneous,

            PlantRsi = PlantRsi,
            PlantIconState = PlantIconState,
            CanScream = CanScream,
            TurnIntoKudzu = TurnIntoKudzu,
            Mutations = new List<RandomPlantMutation>(),

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
            Viable = other.Viable,
            HarvestLogImpact = other.HarvestLogImpact,
            PlantLogImpact = other.PlantLogImpact,
            Name = other.Name,
            Noun = other.Noun,
            DisplayName = other.DisplayName,
            Mysterious = other.Mysterious,

            PacketPrototype = other.PacketPrototype,
            ProductPrototypes = new List<string>(other.ProductPrototypes),
            MutationPrototypes = new List<string>(other.MutationPrototypes),

            Chemicals = new Dictionary<string, SeedChemQuantity>(Chemicals),

            Endurance = Endurance,
            Yield = Yield,
            Lifespan = Lifespan,
            Maturation = Maturation,
            Production = Production,
            GrowthStages = other.GrowthStages,
            HarvestRepeat = HarvestRepeat,
            Potency = Potency,

            Mutations = Mutations,

            Seedless = Seedless,
            Ligneous = Ligneous,

            PlantRsi = other.PlantRsi,
            PlantIconState = other.PlantIconState,
            CanScream = CanScream,
            TurnIntoKudzu = TurnIntoKudzu,

            // Newly cloned seed is unique. No need to unnecessarily clone if repeatedly modified.
            Unique = true,
        };

        // Adding the new chemicals from the new species.
        foreach (var otherChem in other.Chemicals)
        {
            newSeed.Chemicals.TryAdd(otherChem.Key, otherChem.Value);
        }

        // Removing the inherent chemicals from the old species. Leaving mutated/crossbread ones intact.
        foreach (var originalChem in newSeed.Chemicals)
        {
            if (!other.Chemicals.ContainsKey(originalChem.Key) && originalChem.Value.Inherent)
            {
                newSeed.Chemicals.Remove(originalChem.Key);
            }
        }

        // Deep copy growth components from the new species
        foreach (var component in other.GrowthComponents)
        {
            var newComponent = component.DupeComponent();
            newSeed.GrowthComponents.Add(newComponent);
        }

        return newSeed;
    }
}
