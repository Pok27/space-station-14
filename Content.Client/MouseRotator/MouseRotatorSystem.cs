using System;
using Content.Client.E3D.FirstPerson;
using Content.Shared.MouseRotator;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.MouseRotator;

/// <inheritdoc/>
public sealed class MouseRotatorSystem : SharedMouseRotatorSystem
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly FirstPersonInputGateSystem _fpvGate = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted || !_input.MouseScreenPosition.IsValid)
            return;

        var player = _player.LocalEntity;

        if (player == null || !TryComp<MouseRotatorComponent>(player, out var rotator))
            return;

        if (_fpvGate.BlocksMouseRotator(player.Value))
            return;

        var xform = Transform(player.Value);

        var coords = _input.MouseScreenPosition;
        var mapPos = _eye.PixelToMap(coords);

        if (mapPos.MapId == MapId.Nullspace)
            return;

        var angle = (mapPos.Position - _transform.GetMapCoordinates(player.Value, xform: xform).Position).ToWorldAngle();
        var curRot = _transform.GetWorldRotation(xform);

        if (rotator.Simple4DirMode)
        {
            var eyeRot = _eye.CurrentEye.Rotation;
            var angleDir = (angle + eyeRot).GetCardinalDir();
            if (angleDir == (curRot + eyeRot).GetCardinalDir())
                return;

            var rotation = angleDir.ToAngle() - eyeRot;
            if (rotation >= Math.PI)
                rotation -= 2 * Math.PI;
            else if (rotation < -Math.PI)
                rotation += 2 * Math.PI;

            RaisePredictiveEvent(new RequestMouseRotatorRotationEvent
            {
                Rotation = rotation,
                User = GetNetEntity(player)
            });

            return;
        }

        var diff = Angle.ShortestDistance(angle, curRot);
        if (Math.Abs(diff.Theta) < rotator.AngleTolerance.Theta)
            return;

        if (rotator.GoalRotation != null)
        {
            var goalDiff = Angle.ShortestDistance(angle, rotator.GoalRotation.Value);
            if (Math.Abs(goalDiff.Theta) < rotator.AngleTolerance.Theta)
                return;
        }

        RaisePredictiveEvent(new RequestMouseRotatorRotationEvent
        {
            Rotation = angle,
            User = GetNetEntity(player)
        });
    }
}
