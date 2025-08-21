using Robust.Shared.GameObjects;

namespace Content.Shared.DoAfter.Events;

/// <summary>
/// Raised to allow systems to override the NeedHand requirement for a DoAfter.
/// Handlers should set Handled=true and AllowWithoutHands=true to bypass requiring HandsComponent.
/// </summary>
[ByRefEvent]
public record struct DoAfterNeedHandOverrideEvent(EntityUid User, EntityUid? Used)
{
    public readonly EntityUid User = User;
    public readonly EntityUid? Used = Used;

    public bool Handled;
    public bool AllowWithoutHands;
}

