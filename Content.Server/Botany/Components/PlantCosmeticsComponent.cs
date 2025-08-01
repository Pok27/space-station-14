using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Botany.Components;

[RegisterComponent]
[DataDefinition]
public sealed partial class PlantCosmeticsComponent : Component
{
    /// <summary>
    /// The RSI path for the plant sprites.
    /// </summary>
    [DataField(required: true)]
    public ResPath PlantRsi { get; set; } = default!;

    /// <summary>
    /// The icon state for the plant.
    /// </summary>
    [DataField]
    public string PlantIconState { get; set; } = "produce";

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