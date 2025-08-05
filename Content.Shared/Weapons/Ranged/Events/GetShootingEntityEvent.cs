namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Event raised to get the effective shooting entity (whose coordinates and physics should be used).
/// Allows systems to redirect shooting position and recoil (e.g., mech providing itself instead of pilot).
/// This is a general-purpose event not tied to any specific system.
/// </summary>
[ByRefEvent]
public struct GetShootingEntityEvent
{
    /// <summary>
    /// The entity whose coordinates and physics should be used for shooting.
    /// If not set, the original entity will be used.
    /// </summary>
    public EntityUid? ShootingEntity;

    /// <summary>
    /// Whether this event has been handled.
    /// </summary>
    public bool Handled;
}