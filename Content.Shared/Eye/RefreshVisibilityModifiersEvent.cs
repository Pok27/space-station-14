using Robust.Shared.GameObjects;

namespace Content.Shared.Eye;

/// <summary>
/// Raised on an entity to refresh visibility modifiers coming from active status effects.
/// </summary>
[ByRefEvent]
public record struct RefreshVisibilityModifiersEvent
{
    /// <summary>
    /// Visibility layers that should be added by active modifiers.
    /// </summary>
    public ushort AddLayers;

    /// <summary>
    /// Visibility layers that should be removed by active modifiers.
    /// </summary>
    public ushort RemoveLayers;

    /// <summary>
    /// Number of active modifiers that contributed to this refresh.
    /// </summary>
    public int ModifierCount;

    /// <summary>
    /// Adds the specified visibility layer to the refreshed mask.
    /// </summary>
    public void AddLayer(VisibilityFlags layer)
    {
        AddLayers |= (ushort) layer;
    }

    /// <summary>
    /// Removes the specified visibility layer from the refreshed mask.
    /// </summary>
    public void RemoveLayer(VisibilityFlags layer)
    {
        RemoveLayers |= (ushort) layer;
    }
}
