namespace Content.Server.Botany.Components;

[RegisterComponent]
[DataDefinition]
public sealed partial class BasicGrowthComponent : PlantGrowthComponent
{
    private float _waterConsumption = 0.5f;
    private float _nutrientConsumption = 0.75f;

    /// <summary>
    /// Amount of water consumed per growth tick.
    /// </summary>
    [DataField("waterConsumption")]
    public float WaterConsumption
    {
        get => _waterConsumption;
        set => _waterConsumption = value > 0f ? value : 0.5f;
    }

    /// <summary>
    /// Amount of nutrients consumed per growth tick.
    /// </summary>
    [DataField("nutrientConsumption")]
    public float NutrientConsumption
    {
        get => _nutrientConsumption;
        set => _nutrientConsumption = value > 0f ? value : 0.75f;
    }

    /// <summary>
    /// Ensures default values are set after YAML deserialization.
    /// </summary>
    public override void OnValidate()
    {
        base.OnValidate();
        
        // Ensure default values are set if not specified in YAML
        if (_waterConsumption <= 0f)
            _waterConsumption = 0.5f;
            
        if (_nutrientConsumption <= 0f)
            _nutrientConsumption = 0.75f;
    }
}
