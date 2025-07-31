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

/// <summary>
/// Interface for growth component data that can be serialized from YAML.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public partial interface IGrowthComponentData
{
    /// <summary>
    /// Creates a component instance from this data.
    /// </summary>
    PlantGrowthComponent CreateComponent();
}

/// <summary>
/// Data for BasicGrowthComponent that can be serialized from YAML.
/// </summary>
[DataDefinition]
public partial struct BasicGrowthComponentData : IGrowthComponentData
{
    [DataField("WaterConsumption")]
    public float WaterConsumption = 0.5f;

    [DataField("NutrientConsumption")]
    public float NutrientConsumption = 0.75f;

    public PlantGrowthComponent CreateComponent()
    {
        return new BasicGrowthComponent
        {
            WaterConsumption = WaterConsumption,
            NutrientConsumption = NutrientConsumption
        };
    }
}

/// <summary>
/// Data for AtmosphericGrowthComponent that can be serialized from YAML.
/// </summary>
[DataDefinition]
public partial struct AtmosphericGrowthComponentData : IGrowthComponentData
{
    [DataField("IdealHeat")]
    public float IdealHeat = 293f;

    [DataField("HeatTolerance")]
    public float HeatTolerance = 10f;

    [DataField("LowPressureTolerance")]
    public float LowPressureTolerance = 81f;

    [DataField("HighPressureTolerance")]
    public float HighPressureTolerance = 121f;

    public PlantGrowthComponent CreateComponent()
    {
        return new AtmosphericGrowthComponent
        {
            IdealHeat = IdealHeat,
            HeatTolerance = HeatTolerance,
            LowPressureTolerance = LowPressureTolerance,
            HighPressureTolerance = HighPressureTolerance
        };
    }
}

// TODO reduce the number of friends to a reasonable level. Requires ECS-ing things like plant holder component.
[Virtual, DataDefinition]
[Access(typeof(BotanySystem), typeof(PlantHolderSystem), typeof(SeedExtractorSystem), typeof(PlantHolderComponent), typeof(EntityEffectSystem), typeof(MutationSystem))]
public partial class SeedData
{
    #region Tracking

    /// <summary>
    ///     The name of this seed. Determines the name of seed packets.
    /// </summary>
    [DataField]
    public string Name { get; private set; } = "";

    /// <summary>
    ///     The noun for this type of seeds. E.g. for fungi this should probably be "spores" instead of "seeds". Also
    ///     used to determine the name of seed packets.
    /// </summary>
    [DataField]
    public string Noun { get; private set; } = "";

    /// <summary>
    ///     Name displayed when examining the hydroponics tray. Describes the actual plant, not the seed itself.
    /// </summary>
    [DataField]
    public string DisplayName { get; private set; } = "";

    [DataField] public bool Mysterious;

    /// <summary>
    ///     If true, the properties of this seed cannot be modified.
    /// </summary>
    [DataField] public bool Immutable;

    /// <summary>
    ///     If true, there is only a single reference to this seed and it's properties can be directly modified without
    ///     needing to clone the seed.
    /// </summary>
    [ViewVariables]
    public bool Unique = false; // seed-prototypes or yaml-defined seeds for entity prototypes will not generally be unique.
    #endregion

    #region Output
    /// <summary>
    ///     The entity prototype that is spawned when this type of seed is extracted from produce using a seed extractor.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string PacketPrototype = "SeedBase";

    /// <summary>
    ///     The entity prototype this seed spawns when it gets harvested.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
    public List<string> ProductPrototypes = new();

    [DataField] public Dictionary<string, SeedChemQuantity> Chemicals = new();

    #endregion

    #region Tolerances
    [DataField] public float ToxinsTolerance = 4f;

    #endregion

    #region General traits

    [DataField] public float Endurance = 100f;

    [DataField] public int Yield;
    [DataField] public float Lifespan;
    [DataField] public float Maturation;
    [DataField] public float Production;
    [DataField] public int GrowthStages = 6;

    [DataField] public HarvestType HarvestRepeat = HarvestType.NoRepeat;

    [DataField] public float Potency = 1f;

    /// <summary>
    ///     If true, cannot be harvested for seeds. Balances hybrids and
    ///     mutations.
    /// </summary>
    [DataField] public bool Seedless = false;

    /// <summary>
    ///     If true, a sharp tool is required to harvest this plant.
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

    [DataField("screaming")] public bool CanScream;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))] public string KudzuPrototype = "WeakKudzu";

    [DataField] public bool TurnIntoKudzu;
    [DataField] public string? SplatPrototype { get; set; }

    #endregion

    /// <summary>
    /// The mutation effects that have been applied to this plant.
    /// </summary>
    [DataField] public List<RandomPlantMutation> Mutations { get; set; } = new();

    /// <summary>
    ///     The seed prototypes this seed may mutate into when prompted to.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdListSerializer<SeedPrototype>))]
    public List<string> MutationPrototypes = new();

    /// <summary>
    /// The growth component data that will be used to create components for this seed.
    /// </summary>
    [DataField]
    public List<IGrowthComponentData> GrowthComponentData = new();

    /// <summary>
    /// The actual growth components created from the data.
    /// </summary>
    [ViewVariables]
    public List<PlantGrowthComponent> GrowthComponents = new();

    /// <summary>
    /// Whether this seed is viable for growth.
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

    /// <summary>
    /// Initializes the growth components from the component data.
    /// </summary>
    public void InitializeGrowthComponents()
    {
        GrowthComponents.Clear();
        foreach (var componentData in GrowthComponentData)
        {
            GrowthComponents.Add(componentData.CreateComponent());
        }
    }

    public SeedData Clone()
    {
        DebugTools.Assert(!Immutable, "There should be no need to clone an immutable seed.");

        var newSeed = new SeedData
        {
            GrowthComponentData = new List<IGrowthComponentData>(GrowthComponentData),
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

            ToxinsTolerance = ToxinsTolerance,

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
            SplatPrototype = SplatPrototype,
            Mutations = new List<RandomPlantMutation>(),

            // Newly cloned seed is unique. No need to unnecessarily clone if repeatedly modified.
            Unique = true,
        };

        // Initialize growth components from data
        newSeed.InitializeGrowthComponents();

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
            GrowthComponentData = new List<IGrowthComponentData>(other.GrowthComponentData),
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

            ToxinsTolerance = ToxinsTolerance,

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
            SplatPrototype = other.SplatPrototype,

            // Newly cloned seed is unique. No need to unnecessarily clone if repeatedly modified.
            Unique = true,
        };

        // Initialize growth components from data
        newSeed.InitializeGrowthComponents();

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

        return newSeed;
    }
}
