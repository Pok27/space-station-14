using Content.Shared.Atmos;

namespace Content.Server.Botany.Components;

[RegisterComponent]
[DataDefinition]
public sealed partial class ConsumeExudeGasGrowthComponent : PlantGrowthComponent
{
    [DataField] public Dictionary<Gas, float> ConsumeGases = new();
    [DataField] public Dictionary<Gas, float> ExudeGases = new();
}
