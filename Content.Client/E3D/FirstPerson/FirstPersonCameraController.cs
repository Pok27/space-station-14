using System;
using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Client.E3D.FirstPerson;

internal sealed class FirstPersonCameraController
{
    private Angle _lookYaw;
    private bool _lookYawInitialized;
    private Angle _lookPitch = Angle.Zero;

    public Angle LookYaw
    {
        get
        {
            if (!_lookYawInitialized)
                throw new InvalidOperationException("Camera yaw was accessed before initialization.");

            return _lookYaw;
        }
    }

    public Angle LookPitch => _lookPitch;

    public void EnsureYaw(Angle eyeRotation)
    {
        if (_lookYawInitialized)
            return;

        _lookYaw = eyeRotation;
        _lookYawInitialized = true;
    }

    public void ResetYaw(Angle eyeRotation)
    {
        _lookYaw = eyeRotation;
        _lookYawInitialized = true;
    }

    public void ResetPitch()
    {
        _lookPitch = Angle.Zero;
    }

    public void ApplyYawDelta(float degrees, Angle eyeRotation)
    {
        EnsureYaw(eyeRotation);
        _lookYaw = (_lookYaw + Angle.FromDegrees(degrees)).Reduced();
    }

    public void ApplyPitchDelta(float degrees, bool pitchEnabled, float maxPitchDegrees)
    {
        if (!pitchEnabled)
        {
            _lookPitch = Angle.Zero;
            return;
        }

        var pitch = Math.Clamp(_lookPitch.Degrees + degrees, -maxPitchDegrees, maxPitchDegrees);
        _lookPitch = Angle.FromDegrees(pitch);
    }

    public float GetPitchOffsetPixels(float height, float maxPitchDegrees)
    {
        var maxPitch = MathF.Max(1f, maxPitchDegrees);
        var normalized = Math.Clamp((float) (_lookPitch.Degrees / maxPitch), -1f, 1f);
        return -normalized * height * 0.35f;
    }

    public void UpdateFromMouseMotion(Vector2 relativeMotion, float mouseSensitivity, bool invertPitch, bool pitchEnabled, float maxPitchDegrees, Angle eyeRotation)
    {
        ApplyYawDelta(relativeMotion.X * mouseSensitivity, eyeRotation);
        ApplyPitchDelta(relativeMotion.Y * mouseSensitivity * (invertPitch ? -1f : 1f), pitchEnabled, maxPitchDegrees);
    }
}
