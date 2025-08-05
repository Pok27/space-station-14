namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Event raised to get the active weapon entity for a given user.
/// Allows systems to override weapon selection logic (e.g., mechs providing their equipment).
/// </summary>
[ByRefEvent]
public struct GetActiveWeaponEvent
{
    /// <summary>
    /// The weapon entity to use. If not set, the default logic will be used.
    /// </summary>
    public EntityUid? Weapon;

    /// <summary>
    /// Whether this event has been handled.
    /// </summary>
    public bool Handled;
}