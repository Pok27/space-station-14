namespace Content.Shared.Atmos;

/// <summary>
/// Raised on an entity to determine whether it is currently immune to pressure damage.
/// </summary>
[ByRefEvent]
public record struct GetPressureImmunityEvent(bool IsImmune = false, bool Handled = false);
