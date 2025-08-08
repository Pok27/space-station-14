using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Server.Mech.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.FixedPoint;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Handles atmospheric systems for mechs including air circulation, fans, and life support
/// </summary>
public sealed class MechAtmosphereSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly MechSystem _mech = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechComponent, MechAirtightMessage>(OnAirtightMessage);
        SubscribeLocalEvent<MechComponent, MechFanToggleMessage>(OnFanToggleMessage);

        SubscribeLocalEvent<MechPilotComponent, InhaleLocationEvent>(OnInhale);
        SubscribeLocalEvent<MechPilotComponent, ExhaleLocationEvent>(OnExhale);
        SubscribeLocalEvent<MechPilotComponent, AtmosExposedGetAirEvent>(OnExpose);

        // Route filter air requests to mech's installed gas module holder
        SubscribeLocalEvent<MechComponent, GetFilterAirEvent>(OnGetFilterAir);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Update fan energy consumption and gas processing
        var query = EntityQueryEnumerator<MechComponent>();
        while (query.MoveNext(out var uid, out var mechComp))
        {
            var fanModule = GetFanModule(uid, mechComp);
            if (fanModule == null)
                continue;

            if (!fanModule.IsActive)
            {
                fanModule.State = MechFanState.Off;
                Dirty(fanModule.Owner, fanModule);
                continue;
            }

            // If not airtight or missing gas module, fan cannot operate -> Off
            if (!mechComp.Airtight)
            {
                fanModule.State = MechFanState.Off;
                Dirty(fanModule.Owner, fanModule);
                continue;
            }

            GasMixture? internalAir = null;
            foreach (var ent in mechComp.ModuleContainer.ContainedEntities)
            {
                if (TryComp<MechGasCylinderModuleComponent>(ent, out _))
                {
                    if (TryComp<GasTankComponent>(ent, out var tank))
                        internalAir = tank.Air;
                    break;
                }
            }
            if (internalAir == null)
            {
                fanModule.State = MechFanState.Off;
                Dirty(fanModule.Owner, fanModule);
                continue;
            }

            // Determine if external atmosphere is available
            var external = _atmosphere.GetContainingMixture(uid);
            var externalOk = external != null && external.Pressure > 0.5f;

            // Idle if no external gas or internal pressure is at/above external (nothing to intake)
            var internalPressure = internalAir.Pressure;
            var externalPressure = external?.Pressure ?? 0f;
            if (!externalOk || internalPressure >= externalPressure - 0.1f)
            {
                fanModule.State = MechFanState.Idle;
                Dirty(fanModule.Owner, fanModule);
                continue;
            }

            // Process gas: consume energy; if not enough energy, turn off
            var energyConsumption = fanModule.EnergyConsumption * frameTime;
            if (_mech.TryChangeEnergy(uid, -energyConsumption, mechComp))
            {
                fanModule.State = MechFanState.On;

                // Move gas from external to internal based on processing rate (L/s -> fraction of external moles)
                // Use volume ratio to remove a portion of external mixture and merge into internal
                if (external != null)
                {
                    var intakeVolume = MathF.Min(fanModule.GasProcessingRate * frameTime, external.Volume);
                    // Avoid nonsensical volume transfers
                    intakeVolume = MathF.Max(0f, MathF.Min(intakeVolume, internalAir.Volume));
                    if (intakeVolume > 0f)
                    {
                        // Ensure reasonable temperature on internal mix to avoid NaN
                        if (internalAir.Temperature <= 0)
                            internalAir.Temperature = Atmospherics.T20C;

                        var removed = external.RemoveVolume(intakeVolume);
                        if (removed.Temperature <= 0)
                            removed.Temperature = Atmospherics.T20C;
                        _atmosphere.Merge(internalAir, removed);
                    }
                }
            }
            else
            {
                fanModule.IsActive = false;
                fanModule.State = MechFanState.Off;
            }

            Dirty(fanModule.Owner, fanModule);
        }
    }

    private void OnAirtightMessage(EntityUid uid, MechComponent component, MechAirtightMessage args)
    {
        component.Airtight = args.IsAirtight;
        Dirty(uid, component);
        _mech.UpdateUserInterface(uid, component);
    }

    private void OnFanToggleMessage(EntityUid uid, MechComponent component, MechFanToggleMessage args)
    {
        var fanModule = GetFanModule(uid, component);
        if (fanModule == null)
            return;

        fanModule.IsActive = args.IsActive;
        Dirty(fanModule.Owner, fanModule);
        _mech.UpdateUserInterface(uid, component);
    }

    private void OnInhale(EntityUid uid, MechPilotComponent component, InhaleLocationEvent args)
    {
        if (!TryComp<MechComponent>(component.Mech, out var mech))
            return;

        if (!mech.Airtight)
            return;

        if (!_mech.TryGetGasModuleAir(component.Mech, out var air))
            return;

        args.Gas = air;
        _mech.UpdateUserInterface(component.Mech, mech);
    }

    private void OnExhale(EntityUid uid, MechPilotComponent component, ExhaleLocationEvent args)
    {
        if (!TryComp<MechComponent>(component.Mech, out var mech))
            return;

        if (!mech.Airtight)
            return;

        if (!_mech.TryGetGasModuleAir(component.Mech, out var air))
            return;

        args.Gas = air;
        _mech.UpdateUserInterface(component.Mech, mech);
    }

    private void OnExpose(EntityUid uid, MechPilotComponent component, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(component.Mech, out MechComponent? mech))
            return;

        if (mech.Airtight && _mech.TryGetGasModuleAir(component.Mech, out var air))
        {
            args.Handled = true;
            args.Gas = air;
            return;
        }

        args.Gas = _atmosphere.GetContainingMixture(component.Mech, excite: args.Excite);
        args.Handled = true;

        _mech.UpdateUserInterface(component.Mech, mech);
    }

    private void OnGetFilterAir(EntityUid uid, MechComponent component, ref GetFilterAirEvent args)
    {
        if (args.Air != null)
            return;

        // only airtight mechs get internal air
        if (!component.Airtight)
            return;

        if (_mech.TryGetGasModuleAir(uid, out var air))
            args.Air = air;
    }

    private MechFanModuleComponent? GetFanModule(EntityUid mech, MechComponent mechComp)
    {
        foreach (var ent in mechComp.ModuleContainer.ContainedEntities)
        {
            if (TryComp<MechFanModuleComponent>(ent, out var fanModule))
                return fanModule;
        }
        return null;
    }
}
