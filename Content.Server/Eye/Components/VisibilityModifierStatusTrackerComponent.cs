using Robust.Shared.GameObjects;
using Robust.Shared.ViewVariables;

namespace Content.Server.Eye.Components;

/// <summary>
/// Tracks the last visibility modifier delta applied from status effects so refreshes can be recomputed safely.
/// </summary>
[RegisterComponent]
public sealed partial class VisibilityModifierStatusTrackerComponent : Component
{
    /// <summary>
    /// Visibility layers added by the last refresh.
    /// </summary>
    [ViewVariables]
    public ushort LastAddedLayers;

    /// <summary>
    /// Visibility layers removed by the last refresh.
    /// </summary>
    [ViewVariables]
    public ushort LastRemovedLayers;
}
