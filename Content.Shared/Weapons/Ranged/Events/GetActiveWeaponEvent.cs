namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Event raised to get the active weapon entity for a given user.
/// Allows systems to override weapon selection logic (e.g., mechs providing their equipment).
/// </summary>
public sealed class GetActiveWeaponEvent : HandledEntityEventArgs
{
    /// <summary>
    /// The weapon entity to use. If not set, the default logic will be used.
    /// </summary>
    public EntityUid? Weapon { get; set; }

    public GetActiveWeaponEvent()
    {
    }
}