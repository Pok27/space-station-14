using Robust.Shared.Log;

namespace Content.Server.Botany.Components;

[RegisterComponent]
[DataDefinition]
public sealed partial class BasicGrowthComponent : PlantGrowthComponent
{
    /// <summary>
    /// Amount of water consumed per growth tick.
    /// </summary>
    [DataField]
    public float WaterConsumption = 0.5f;

    /// <summary>
    /// Amount of nutrients consumed per growth tick.
    /// </summary>
    [DataField]
    public float NutrientConsumption = 0.75f;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info($"BasicGrowthComponent initialized: WaterConsumption={WaterConsumption}, NutrientConsumption={NutrientConsumption}");
    }
}
