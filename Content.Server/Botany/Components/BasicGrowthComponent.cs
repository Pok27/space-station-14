namespace Content.Server.Botany.Components;

[RegisterComponent]
public sealed partial class BasicGrowthComponent : PlantGrowthComponent
{
    /// <summary>
    /// Amount of water consumed per growth tick.
    /// </summary>
    [DataField]
    public float waterConsumption = 0.5f;

    /// <summary>
    /// Amount of nutrients consumed per growth tick.
    /// </summary>
    [DataField]
    public float nutrientConsumption = 0.75f;
}
