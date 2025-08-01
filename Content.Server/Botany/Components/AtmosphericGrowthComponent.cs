namespace Content.Server.Botany.Components;

[RegisterComponent]
[DataDefinition]
public sealed partial class AtmosphericGrowthComponent : PlantGrowthComponent
{
    private float _idealHeat = 293f;
    private float _heatTolerance = 10f;
    private float _lowPressureTolerance = 81f;
    private float _highPressureTolerance = 121f;

    /// <summary>
    /// Ideal temperature for plant growth in Kelvin.
    /// </summary>
    [DataField("idealHeat")]
    public float IdealHeat
    {
        get => _idealHeat;
        set => _idealHeat = value > 0f ? value : 293f;
    }

    /// <summary>
    /// Temperature tolerance range around ideal heat.
    /// </summary>
    [DataField("heatTolerance")]
    public float HeatTolerance
    {
        get => _heatTolerance;
        set => _heatTolerance = value > 0f ? value : 10f;
    }

    /// <summary>
    /// Minimum pressure tolerance for plant growth.
    /// </summary>
    [DataField("lowPressureTolerance")]
    public float LowPressureTolerance
    {
        get => _lowPressureTolerance;
        set => _lowPressureTolerance = value > 0f ? value : 81f;
    }

    /// <summary>
    /// Maximum pressure tolerance for plant growth.
    /// </summary>
    [DataField("highPressureTolerance")]
    public float HighPressureTolerance
    {
        get => _highPressureTolerance;
        set => _highPressureTolerance = value > 0f ? value : 121f;
    }

    /// <summary>
    /// Ensures default values are set after YAML deserialization.
    /// </summary>
    public override void OnValidate()
    {
        base.OnValidate();
        
        // Ensure default values are set if not specified in YAML
        if (_idealHeat <= 0f)
            _idealHeat = 293f;
            
        if (_heatTolerance <= 0f)
            _heatTolerance = 10f;
            
        if (_lowPressureTolerance <= 0f)
            _lowPressureTolerance = 81f;
            
        if (_highPressureTolerance <= 0f)
            _highPressureTolerance = 121f;
    }
}
