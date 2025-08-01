namespace Content.Server.Botany.Systems;

/// <summary>
/// Harvest options for plants.
/// </summary>
public enum HarvestType
{
    /// <summary>
    /// Plant is removed on harvest.
    /// </summary>
    NoRepeat,
    
    /// <summary>
    /// Plant makes produce every Production ticks.
    /// </summary>
    Repeat,
    
    /// <summary>
    /// Repeat, plus produce is dropped on the ground near the plant automatically.
    /// </summary>
    SelfHarvest
}