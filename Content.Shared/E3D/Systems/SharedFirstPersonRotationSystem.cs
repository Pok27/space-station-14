using Content.Shared.E3D.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared.E3D.Systems;

public sealed class SharedFirstPersonRotationSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<RequestFirstPersonRotationEvent>(OnRotationRequest);
    }

    private void OnRotationRequest(RequestFirstPersonRotationEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity != GetEntity(msg.User))
            return;

        if (args.SenderSession.AttachedEntity is not { } attached ||
            !TryComp<FirstPersonViewComponent>(attached, out _) ||
            !TryComp<InputMoverComponent>(attached, out var mover))
        {
            return;
        }

        var moverRotation = GetMoverViewRotation(attached, mover, msg.ViewRotation);
        if (mover.RelativeRotation.Equals(moverRotation) && mover.TargetRelativeRotation.Equals(moverRotation))
            return;

        mover.RelativeRotation = moverRotation;
        mover.TargetRelativeRotation = moverRotation;
        Dirty(attached, mover);
    }

    private Angle GetMoverViewRotation(EntityUid uid, InputMoverComponent mover, Angle viewYaw)
    {
        var parentRotation = Angle.Zero;
        var relative = mover.RelativeEntity;

        if (relative == null)
        {
            var xform = Transform(uid);
            relative = xform.GridUid ?? xform.MapUid;
        }

        if (relative != null && TryComp(relative.Value, out TransformComponent? relativeXform))
            parentRotation = _transform.GetWorldRotation(relativeXform);

        return (-(viewYaw + parentRotation)).FlipPositive();
    }
}

[Serializable, NetSerializable]
public sealed class RequestFirstPersonRotationEvent : EntityEventArgs
{
    public Angle ViewRotation;
    public NetEntity? User;
}
