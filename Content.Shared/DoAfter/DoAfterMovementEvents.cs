using Robust.Shared.GameStates;

namespace Content.Shared.DoAfter;

/// <summary>
/// Raised by-ref on the user to allow systems to override which entity should be treated as the "user"
/// for DoAfter movement cancellation checks.
/// </summary>
[ByRefEvent]
public struct GetDoAfterUserEvent(EntityUid user)
{
    public EntityUid User = user;
}

