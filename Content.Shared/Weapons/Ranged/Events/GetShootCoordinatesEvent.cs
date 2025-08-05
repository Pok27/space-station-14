namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Event raised to get the coordinates for shooting.
/// Allows systems to override shooting position (e.g., mechs providing their position instead of pilot position).
/// </summary>
[ByRefEvent]
public struct GetShootCoordinatesEvent
{
    /// <summary>
    /// The coordinates to shoot from. If not set, the default user coordinates will be used.
    /// </summary>
    public EntityCoordinates? Coordinates;

    /// <summary>
    /// Whether this event has been handled.
    /// </summary>
    public bool Handled;
}