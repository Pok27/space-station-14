using Content.Server.Mech.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.FixedPoint;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Vehicle;
using System.Numerics;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Handles per-frame movement energy drain for mechs to avoid an Update override in MechSystem.
/// </summary>
public sealed class MechMovementEnergySystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly VehicleSystem _vehicle = default!;
    [Dependency] private readonly MechSystem _mechSystem = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerate = EntityQueryEnumerator<MechComponent, InputMoverComponent>();
        while (enumerate.MoveNext(out var mechUid, out var mech, out var mover))
        {
            if (mech.MovementEnergyPerSecond <= 0f)
                continue;

            if (!_vehicle.HasOperator(mechUid))
                continue;

            if (!mover.CanMove)
                continue;

            if (mover.WishDir == Vector2.Zero)
                continue;

            var toDrain = mech.MovementEnergyPerSecond * frameTime;
            if (toDrain <= 0f)
                continue;

            _mechSystem.TryChangeEnergy(mechUid, -FixedPoint2.New(toDrain), mech);
            _actionBlocker.UpdateCanMove(mechUid);
        }
    }
}

