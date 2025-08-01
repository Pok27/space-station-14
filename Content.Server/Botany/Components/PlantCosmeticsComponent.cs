using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Botany.Components;

[RegisterComponent]
[DataDefinition]
public sealed partial class PlantCosmeticsComponent : PlantGrowthComponent
{
    /// <summary>
    /// Screams random sound from collection SoundCollectionSpecifier
    /// </summary>
    [DataField]
    public SoundSpecifier ScreamSound = new SoundCollectionSpecifier("PlantScreams", AudioParams.Default.WithVolume(-10));

    /// <summary>
    /// If true, AAAAAAAAAAAHHHHHHHHHHH!
    /// </summary>
    [DataField("screaming")]
    public bool CanScream;

    /// <summary>
    /// Which kind of kudzu this plant will turn into if it kuzuifies.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string KudzuPrototype = "WeakKudzu";

    /// <summary>
    /// If true, this plant turns into it's KudzuPrototype when the PlantHolder's WeedLevel hits this plant's WeedHighLevelThreshold.
    /// </summary>
    [DataField]
    public bool TurnIntoKudzu;
}