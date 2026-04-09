using Content.Shared.E3D.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared.E3D.Systems;

public sealed class SharedFirstPersonRotationSystem : EntitySystem
{
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

        var moverRotation = (-msg.ViewRotation).FlipPositive();
        if (mover.RelativeRotation.Equals(moverRotation) && mover.TargetRelativeRotation.Equals(moverRotation))
            return;

        mover.RelativeRotation = moverRotation;
        mover.TargetRelativeRotation = moverRotation;
        Dirty(attached, mover);
    }
}

[Serializable, NetSerializable]
public sealed class RequestFirstPersonRotationEvent : EntityEventArgs
{
    public Angle ViewRotation;
    public NetEntity? User;
}
