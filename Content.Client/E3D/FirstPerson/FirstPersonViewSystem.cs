using Content.Shared.E3D.Systems;
using Content.Shared.E3D;
using Content.Shared.E3D.Components;
using Content.Shared.Movement.Components;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.Client.E3D.FirstPerson;

public sealed class FirstPersonViewSystem : EntitySystem
{
    private const double YawSyncEpsilonRadians = 0.0001;

    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    private (float EyeHeight, float FovDegrees, float MaxDistance, bool PitchEnabled, float InteractionDistance, int ColumnStep, bool FloorEnabled, bool BillboardEnabled, FirstPersonLightingMode LightingMode, FirstPersonQualityPreset QualityPreset, int LogicalColumns, int MaxBillboards, bool EnableFloorPass)? _lastApplied;
    private Angle? _lastSentYaw;

    public bool IsActive => Controller.Enabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalDetached);

        SubscribeLocalEvent<FirstPersonViewComponent, ComponentStartup>(OnFpvStartup);
        SubscribeLocalEvent<FirstPersonViewComponent, ComponentShutdown>(OnFpvShutdown);
    }

    private FirstPersonUIController Controller => _ui.GetUIController<FirstPersonUIController>();

    private void OnLocalAttached(LocalPlayerAttachedEvent args)
    {
        RefreshEnabled(args.Entity);
    }

    private void OnLocalDetached(LocalPlayerDetachedEvent args)
    {
        Controller.SetEnabled(false);
        _systems.GetEntitySystem<FirstPersonInteractionSystem>().Clear();
        _lastApplied = null;
        _lastSentYaw = null;
    }

    private void OnFpvStartup(Entity<FirstPersonViewComponent> ent, ref ComponentStartup args)
    {
        if (ent.Owner == _player.LocalEntity)
            ApplySettings(ent.Comp);
    }

    private void OnFpvShutdown(Entity<FirstPersonViewComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner == _player.LocalEntity)
            Controller.SetEnabled(false);
        _systems.GetEntitySystem<FirstPersonInteractionSystem>().Clear();
        _lastApplied = null;
        _lastSentYaw = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is not { } local)
            return;

        if (!TryComp(local, out FirstPersonViewComponent? fpv))
            return;

        var cur = (fpv.EyeHeight, fpv.FovDegrees, fpv.MaxDistance, fpv.PitchEnabled, fpv.InteractionDistance, fpv.ColumnStep, fpv.FloorEnabled, fpv.BillboardEnabled, fpv.LightingMode, fpv.QualityPreset, fpv.LogicalColumns, fpv.MaxBillboards, fpv.EnableFloorPass);
        if (_lastApplied == null || !_lastApplied.Value.Equals(cur))
            ApplySettings(fpv);

        if (!Controller.Enabled || !Controller.TryGetControl(out var view))
            return;

        var yaw = view.LookYaw;
        if (_lastSentYaw != null && Angle.ShortestDistance(_lastSentYaw.Value, yaw).EqualsApprox(Angle.Zero, YawSyncEpsilonRadians))
            return;

        if (!TryComp<InputMoverComponent>(local, out var mover))
            return;

        var moverRotation = (-yaw).FlipPositive();
        mover.RelativeRotation = moverRotation;
        mover.TargetRelativeRotation = moverRotation;

        RaisePredictiveEvent(new RequestFirstPersonRotationEvent
        {
            User = GetNetEntity(local),
            ViewRotation = yaw,
        });

        _lastSentYaw = yaw;
    }

    private void RefreshEnabled(EntityUid attached)
    {
        if (!TryComp(attached, out FirstPersonViewComponent? fpv))
        {
            Controller.SetEnabled(false);
            _systems.GetEntitySystem<FirstPersonInteractionSystem>().Clear();
            _lastApplied = null;
            _lastSentYaw = null;
            return;
        }

        ApplySettings(fpv);
        Controller.SetEnabled(true);

        if (Controller.TryGetControl(out var view))
            view.ResetLookYawToEye();
    }

    private void ApplySettings(FirstPersonViewComponent fpv)
    {
        var ctrl = Controller;
        if (ctrl.TryGetControl(out var view))
        {
            view.FovDegrees = fpv.FovDegrees;
            view.MaxDistance = fpv.MaxDistance;
            view.EyeHeight = fpv.EyeHeight;
            view.PitchEnabled = fpv.PitchEnabled;
            view.InteractionDistance = fpv.InteractionDistance;
            view.ColumnStep = fpv.ColumnStep;
            view.FloorEnabled = fpv.FloorEnabled;
            view.BillboardEnabled = fpv.BillboardEnabled;
            view.LightingMode = fpv.LightingMode;
            view.QualityPreset = fpv.QualityPreset;
            view.LogicalColumns = fpv.LogicalColumns;
            view.MaxBillboards = fpv.MaxBillboards;
            view.EnableFloorPass = fpv.EnableFloorPass;
        }

        _lastApplied = (fpv.EyeHeight, fpv.FovDegrees, fpv.MaxDistance, fpv.PitchEnabled, fpv.InteractionDistance, fpv.ColumnStep, fpv.FloorEnabled, fpv.BillboardEnabled, fpv.LightingMode, fpv.QualityPreset, fpv.LogicalColumns, fpv.MaxBillboards, fpv.EnableFloorPass);
    }
}

