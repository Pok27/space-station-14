namespace Content.Server.Botany.Components;

[RegisterComponent]
[DataDefinition]
public sealed partial class AtmosphericGrowthComponent : PlantGrowthComponent
{
    /// <summary>
    /// Ideal temperature for plant growth in Kelvin.
    /// </summary>
    [DataField("idealHeat")]
    public float IdealHeat = 293f;

    /// <summary>
    /// Temperature tolerance range around ideal heat.
    /// </summary>
    [DataField("heatTolerance")]
    public float HeatTolerance = 10f;

    /// <summary>
    /// Minimum pressure tolerance for plant growth.
    /// </summary>
    [DataField("lowPressureTolerance")]
    public float LowPressureTolerance = 81f;

    /// <summary>
    /// Maximum pressure tolerance for plant growth.
    /// </summary>
    [DataField("lighPressureTolerance")]
    public float HighPressureTolerance = 121f;

    /// <summary>
    /// Ensures default values are set after YAML deserialization.
    /// </summary>
    public override void OnValidate()
    {
        base.OnValidate();
        
        // Ensure default values are set if not specified in YAML
        if (IdealHeat == 0f)
            IdealHeat = 293f;
            
        if (HeatTolerance == 0f)
            HeatTolerance = 10f;
            
        if (LowPressureTolerance == 0f)
            LowPressureTolerance = 81f;
            
        if (HighPressureTolerance == 0f)
            HighPressureTolerance = 121f;
    }
}
