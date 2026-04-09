using System;
using System.Numerics;
using Content.Shared.E3D;
using Content.Shared.E3D.Components;
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
    private const float DefaultMouseSensitivity = 0.16f;
    private const float DefaultMaxPitchDegrees = 70f;
    private const float DefaultCursorTurnSpeedDegrees = 220f;
    private const float DefaultCursorTurnDeadZoneFraction = 0.08f;

    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystems = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private readonly FirstPersonCameraController _camera = new();

    private FirstPersonSceneBuilderSystem SceneBuilder => _entitySystems.GetEntitySystem<FirstPersonSceneBuilderSystem>();
    private FirstPersonInteractionSystem Interaction => _entitySystems.GetEntitySystem<FirstPersonInteractionSystem>();
    private FirstPersonRenderPipelineSystem RenderPipeline => _entitySystems.GetEntitySystem<FirstPersonRenderPipelineSystem>();

    public float FovDegrees { get; set; } = FirstPersonViewDefaults.DefaultFovDegrees;
    public float MaxDistance { get; set; } = FirstPersonViewDefaults.DefaultMaxDistance;
    public float EyeHeight { get; set; } = FirstPersonViewDefaults.DefaultEyeHeight;
    public float InteractionDistance { get; set; } = FirstPersonViewDefaults.DefaultInteractionDistance;
    public bool DrawCrosshair { get; set; } = true;
    public float MouseSensitivity { get; set; } = DefaultMouseSensitivity;
    public bool InvertPitch { get; set; }
    public float MaxPitchDegrees { get; set; } = DefaultMaxPitchDegrees;
    public float CursorTurnSpeedDegrees { get; set; } = DefaultCursorTurnSpeedDegrees;
    public float CursorTurnDeadZoneFraction { get; set; } = DefaultCursorTurnDeadZoneFraction;
    public int ColumnStep { get; set; } = FirstPersonViewDefaults.DefaultColumnStep;
    public bool FloorEnabled { get; set; } = FirstPersonViewDefaults.DefaultFloorEnabled;
    public bool BillboardEnabled { get; set; } = FirstPersonViewDefaults.DefaultBillboardEnabled;
    public bool PitchEnabled { get; set; } = FirstPersonViewDefaults.DefaultPitchEnabled;
    public FirstPersonLightingMode LightingMode { get; set; } = FirstPersonViewDefaults.DefaultLightingMode;
    public FirstPersonQualityPreset QualityPreset { get; set; } = FirstPersonViewDefaults.DefaultQualityPreset;
    public int LogicalColumns { get; set; } = FirstPersonViewDefaults.DefaultLogicalColumns;
    public int MaxBillboards { get; set; } = FirstPersonViewDefaults.DefaultMaxBillboards;
    public bool EnableFloorPass { get; set; } = FirstPersonViewDefaults.DefaultEnableFloorPass;

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
