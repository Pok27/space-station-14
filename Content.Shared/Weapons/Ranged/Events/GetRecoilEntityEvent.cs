namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Event raised to get the entity that should receive recoil impulse.
/// Allows systems to override recoil target (e.g., mechs receiving recoil instead of pilot).
/// </summary>
[ByRefEvent]
public struct GetRecoilEntityEvent
{
    /// <summary>
    /// The entity that should receive the recoil impulse. If not set, the default user will be used.
    /// </summary>
    public EntityUid? Entity;

    /// <summary>
    /// Whether this event has been handled.
    /// </summary>
    public bool Handled;
}