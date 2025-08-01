namespace Content.Server.Botany.Components;

[RegisterComponent]
[DataDefinition]
public sealed partial class BasicGrowthComponent : PlantGrowthComponent
{
    /// <summary>
    /// Amount of water consumed per growth tick.
    /// </summary>
    [DataField("waterConsumption")]
    public float WaterConsumption = 0.5f;

    /// <summary>
    /// Amount of nutrients consumed per growth tick.
    /// </summary>
    [DataField("nutrientConsumption")]
    public float NutrientConsumption = 0.75f;

    /// <summary>
    /// Ensures default values are set after YAML deserialization.
    /// </summary>
    public override void OnValidate()
    {
        base.OnValidate();
        
        // Ensure default values are set if not specified in YAML
        if (WaterConsumption == 0f)
            WaterConsumption = 0.5f;
            
        if (NutrientConsumption == 0f)
            NutrientConsumption = 0.75f;
    }
}
