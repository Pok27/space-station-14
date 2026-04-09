using System;
using System.Numerics;
using Content.Shared.E3D;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.E3D.FirstPerson;

public sealed class FirstPersonViewControl : Control, IViewportControl
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystems = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private readonly FirstPersonCameraController _camera = new();

    private FirstPersonSceneBuilderSystem SceneBuilder => _entitySystems.GetEntitySystem<FirstPersonSceneBuilderSystem>();
    private FirstPersonInteractionSystem Interaction => _entitySystems.GetEntitySystem<FirstPersonInteractionSystem>();
    private FirstPersonRenderPipelineSystem RenderPipeline => _entitySystems.GetEntitySystem<FirstPersonRenderPipelineSystem>();

    public float FovDegrees { get; set; } = 100f;
    public float MaxDistance { get; set; } = 18f;
    public float EyeHeight { get; set; } = 0.82f;
    public float InteractionDistance { get; set; } = 2.5f;
    public bool DrawCrosshair { get; set; } = true;
    public float MouseSensitivity { get; set; } = 0.16f;
    public bool InvertPitch { get; set; }
    public float MaxPitchDegrees { get; set; } = 70f;
    public float CursorTurnSpeedDegrees { get; set; } = 220f;
    public float CursorTurnDeadZoneFraction { get; set; } = 0.08f;
    public int ColumnStep { get; set; } = 2;
    public bool FloorEnabled { get; set; } = true;
    public bool BillboardEnabled { get; set; } = true;
    public bool PitchEnabled { get; set; } = true;
    public FirstPersonLightingMode LightingMode { get; set; } = FirstPersonLightingMode.DistanceFog;
    public FirstPersonQualityPreset QualityPreset { get; set; } = FirstPersonQualityPreset.CorrectnessLow;
    public int LogicalColumns { get; set; } = 160;
    public int MaxBillboards { get; set; } = 16;
    public bool EnableFloorPass { get; set; } = true;

    public FirstPersonViewControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        CanKeyboardFocus = true;
        KeyboardFocusOnClick = true;
    }

    public IClydeWindow? Window => _uiManager.RootControl.Window;

    public Angle LookYaw
    {
        get
        {
            _camera.EnsureYaw(_eyeManager.CurrentEye.Rotation);
            return _camera.LookYaw;
        }
    }

    public Angle LookPitch => _camera.LookPitch;

    public void ResetLookYawToEye()
    {
        _camera.ResetYaw(_eyeManager.CurrentEye.Rotation);
    }

    public void ResetLookPitch()
    {
        _camera.ResetPitch();
    }

    public float GetHorizonOffsetPixels(float height)
    {
        return _camera.GetPitchOffsetPixels(height, MaxPitchDegrees);
    }

    private FpvCameraState BuildCameraState()
    {
        return SceneBuilder.BuildCameraState(this);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Handled)
            return;

        _input.ViewportKeyEvent(this, args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Handled)
            return;

        _input.ViewportKeyEvent(this, args);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!Visible || !HasKeyboardFocus())
            return;

        _camera.UpdateFromMouseMotion(
            args.Relative,
            MouseSensitivity,
            InvertPitch,
            PitchEnabled,
            MaxPitchDegrees,
            _eyeManager.CurrentEye.Rotation);
    }

    protected override void VisibilityChanged(bool newVisible)
    {
        base.VisibilityChanged(newVisible);

        if (newVisible)
        {
            GrabKeyboardFocus();
            ResetLookYawToEye();
        }
        else
        {
            Interaction.Clear();
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!Visible || !HasKeyboardFocus() || !_input.MouseScreenPosition.IsValid)
            return;

        var localMouse = _input.MouseScreenPosition.Position - GlobalPixelPosition;
        var size = (Vector2) PixelSize;
        if (size.X <= 1f || size.Y <= 1f)
            return;

        var center = size / 2f;
        var offset = localMouse - center;
        var normalized = new Vector2(
            offset.X / MathF.Max(1f, center.X),
            offset.Y / MathF.Max(1f, center.Y));

        _camera.UpdateFromCursorTurn(
            normalized,
            CursorTurnDeadZoneFraction,
            CursorTurnSpeedDegrees,
            args.DeltaSeconds,
            InvertPitch,
            PitchEnabled,
            MaxPitchDegrees,
            _eyeManager.CurrentEye.Rotation);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var camera = BuildCameraState();
        var horizon = PixelSize.Y / 2f + GetHorizonOffsetPixels(PixelSize.Y);
        RenderPipeline.DrawFrame(this, handle, camera, horizon);
    }

    public MapCoordinates ScreenToMap(Vector2 coords)
    {
        return PixelToMap(coords);
    }

    public MapCoordinates PixelToMap(Vector2 point)
    {
        return RenderPipeline.PixelToMap(this, BuildCameraState(), point);
    }

    public Vector2 WorldToScreen(Vector2 map)
    {
        return RenderPipeline.WorldToScreen(this, BuildCameraState(), map);
    }

    public Matrix3x2 GetWorldToScreenMatrix()
    {
        return Matrix3x2.Identity;
    }

    public Matrix3x2 GetLocalToScreenMatrix()
    {
        return Matrix3Helpers.CreateTransform(GlobalPixelPosition, 0, Vector2.One);
    }
}
