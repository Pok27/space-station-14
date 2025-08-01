using Robust.Shared.GameObjects;

namespace Content.Server.Botany.Components;

[RegisterComponent]
[DataDefinition]
public sealed partial class WeedPestToxinsComponent : PlantGrowthComponent
{
    [DataField("toxinsTolerance")]
    public float ToxinsTolerance = 4f;
}