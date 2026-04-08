using Robust.Shared.Map;
using Robust.Shared.GameObjects;

namespace Content.Client.E3D.FirstPerson;

public sealed class FirstPersonInteractionSystem : EntitySystem
{
    public bool Active { get; private set; }
    public FpvInteractionHit? CurrentHit { get; private set; }

    public void SetInteractionHit(FpvInteractionHit? hit)
    {
        CurrentHit = hit;
        Active = hit != null;
    }

    public void Clear()
    {
        Active = false;
        CurrentHit = null;
    }

    public bool TryGetCurrentHit(out FpvInteractionHit hit)
    {
        if (CurrentHit is not { } current)
        {
            hit = default;
            return false;
        }

        hit = current;
        return true;
    }

    public MapCoordinates GetAimCoordinatesOr(MapCoordinates fallback)
    {
        return CurrentHit?.Coordinates ?? fallback;
    }

    public EntityUid? GetTargetOr(EntityUid? fallback)
    {
        return CurrentHit?.Target ?? fallback;
    }
}
