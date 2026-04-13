using System.Numerics;
using Content.Shared.E3D;
using Content.Shared.E3D.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Map;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.E3D.FirstPerson;

public sealed class FirstPersonViewControl : Control, IViewportControl
{
    private const float DefaultMouseSensitivity = 0.16f;
    private const float DefaultMaxPitchDegrees = 70f;

    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystems = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    private readonly FirstPersonCameraController _camera = new();
    private MoveButtons _fpvSourceButtons;
    private MoveButtons _fpvEmittedButtons;

    private FirstPersonSceneBuilderSystem SceneBuilder => _entitySystems.GetEntitySystem<FirstPersonSceneBuilderSystem>();
    private FirstPersonInteractionSystem Interaction => _entitySystems.GetEntitySystem<FirstPersonInteractionSystem>();
    private InputSystem InputSystem => _entitySystems.GetEntitySystem<InputSystem>();
    private FirstPersonRenderPipelineSystem RenderPipeline => _entitySystems.GetEntitySystem<FirstPersonRenderPipelineSystem>();
    private SharedTransformSystem TransformSystem => _entitySystems.GetEntitySystem<SharedTransformSystem>();

    public float FovDegrees { get; set; } = FirstPersonViewDefaults.DefaultFovDegrees;
    public float MaxDistance { get; set; } = FirstPersonViewDefaults.DefaultMaxDistance;
    public float EyeHeight { get; set; } = FirstPersonViewDefaults.DefaultEyeHeight;
    public float InteractionDistance { get; set; } = FirstPersonViewDefaults.DefaultInteractionDistance;
    public bool DrawCrosshair { get; set; } = true;
    public float MouseSensitivity { get; set; } = DefaultMouseSensitivity;
    public bool InvertPitch { get; set; }
    public float MaxPitchDegrees { get; set; } = DefaultMaxPitchDegrees;
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

    public void SyncLookYawFromMob()
    {
        if (_player.LocalEntity is not { } mob)
        {
            ResetLookYawToEye();
            return;
        }

        _camera.ResetYaw(TransformSystem.GetWorldRotation(mob));
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

        if (HandleFirstPersonMovementInput(args, true))
            return;

        _input.ViewportKeyEvent(this, args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Handled)
            return;

        if (HandleFirstPersonMovementInput(args, false))
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

        if (_fpvSourceButtons != MoveButtons.None)
            UpdateFirstPersonMovement(args.GlobalPixelPosition);
    }

    protected override void VisibilityChanged(bool newVisible)
    {
        base.VisibilityChanged(newVisible);

        if (newVisible)
        {
            GrabKeyboardFocus();
            SyncLookYawFromMob();
        }
        else
        {
            ReleaseFirstPersonMovement();
            Interaction.Clear();
        }

        UpdateRelativeMouseMode();
    }

    protected override void KeyboardFocusEntered()
    {
        base.KeyboardFocusEntered();
        UpdateRelativeMouseMode();
    }

    protected override void KeyboardFocusExited()
    {
        base.KeyboardFocusExited();
        ReleaseFirstPersonMovement();
        UpdateRelativeMouseMode();
    }

    private void UpdateRelativeMouseMode()
    {

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

    private bool HandleFirstPersonMovementInput(GUIBoundKeyEventArgs args, bool pressed)
    {
        var sourceBit = MoveButtons.None;

        if (args.Function == EngineKeyFunctions.MoveUp)
            sourceBit = MoveButtons.Up;
        else if (args.Function == EngineKeyFunctions.MoveDown)
            sourceBit = MoveButtons.Down;
        else if (args.Function == EngineKeyFunctions.MoveLeft)
            sourceBit = MoveButtons.Left;
        else if (args.Function == EngineKeyFunctions.MoveRight)
            sourceBit = MoveButtons.Right;

        if (sourceBit == MoveButtons.None)
            return false;

        if (pressed)
            _fpvSourceButtons |= sourceBit;
        else
            _fpvSourceButtons &= ~sourceBit;

        UpdateFirstPersonMovement(args.PointerLocation);
        args.Handle();
        return true;
    }

    private void UpdateFirstPersonMovement(ScreenCoordinates pointerLocation)
    {
        var targetButtons = CalculateViewRelativeButtons(_fpvSourceButtons);
        var changed = _fpvEmittedButtons ^ targetButtons;

        SendMovementButtonDelta(changed, targetButtons, MoveButtons.Up, EngineKeyFunctions.MoveUp, pointerLocation);
        SendMovementButtonDelta(changed, targetButtons, MoveButtons.Down, EngineKeyFunctions.MoveDown, pointerLocation);
        SendMovementButtonDelta(changed, targetButtons, MoveButtons.Left, EngineKeyFunctions.MoveLeft, pointerLocation);
        SendMovementButtonDelta(changed, targetButtons, MoveButtons.Right, EngineKeyFunctions.MoveRight, pointerLocation);

        _fpvEmittedButtons = targetButtons;
    }

    private MoveButtons CalculateViewRelativeButtons(MoveButtons sourceButtons)
    {
        var leftHeld = (sourceButtons & MoveButtons.Left) != 0;
        var rightHeld = (sourceButtons & MoveButtons.Right) != 0;
        var upHeld = (sourceButtons & MoveButtons.Up) != 0;
        var downHeld = (sourceButtons & MoveButtons.Down) != 0;

        var strafe = leftHeld == rightHeld ? 0 : leftHeld ? -1 : 1;

        var forward = upHeld == downHeld ? 0 : upHeld ? 1 : -1;

        if (strafe == 0 && forward == 0)
            return MoveButtons.None;

        var adjustedYaw = LookYaw - Angle.FromDegrees(45f);
        var forwardDir = -adjustedYaw.ToWorldVec().GetDir().ToIntVec();
        var rightDir = new Vector2i(forwardDir.Y, -forwardDir.X);
        var dir = forwardDir * forward + rightDir * strafe;

        var result = MoveButtons.None;

        if (dir.Y > 0)
            result |= MoveButtons.Up;
        else if (dir.Y < 0)
            result |= MoveButtons.Down;

        if (dir.X > 0)
            result |= MoveButtons.Right;
        else if (dir.X < 0)
            result |= MoveButtons.Left;

        return result;
    }

    private void SendMovementButtonDelta(
        MoveButtons changed,
        MoveButtons targetButtons,
        MoveButtons bit,
        BoundKeyFunction function,
        ScreenCoordinates pointerLocation)
    {
        if ((changed & bit) == 0)
            return;

        if (_player.LocalEntity is not { } player)
            return;

        var funcId = _input.NetworkBindMap.KeyFunctionID(function);
        var coords = TransformSystem.ToCoordinates(player, TransformSystem.GetMapCoordinates(player));
        var state = (targetButtons & bit) != 0 ? BoundKeyState.Down : BoundKeyState.Up;
        var message = new ClientFullInputCmdMessage(
            _timing.CurTick,
            _timing.TickFraction,
            funcId,
            coords,
            pointerLocation,
            state,
            EntityUid.Invalid);

        InputSystem.HandleInputCommand(_player.LocalSession, function, message);
    }

    private void ReleaseFirstPersonMovement()
    {
        if (_fpvSourceButtons == MoveButtons.None && _fpvEmittedButtons == MoveButtons.None)
            return;

        _fpvSourceButtons = MoveButtons.None;
        UpdateFirstPersonMovement(ScreenCoordinates.Invalid);
    }
}
