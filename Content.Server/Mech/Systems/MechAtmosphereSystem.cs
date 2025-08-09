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
            var uiDirty = false;

            // Tick purge cooldown
            if (TryComp<MechCabinPurgeComponent>(uid, out var purge))
            {
                if (purge.CooldownRemaining > 0)
                {
                    purge.CooldownRemaining -= frameTime;
                    if (purge.CooldownRemaining <= 0)
                        RemCompDeferred<MechCabinPurgeComponent>(uid);
                }
            }

            var fanModule = GetFanModule(uid, mechComp);
            if (fanModule != null)
            {
                if (!fanModule.IsActive)
                {
                    fanModule.State = MechFanState.Off;
                    Dirty(fanModule.Owner, fanModule);
                }
                else
                {
                    // Fans operate independently of airtight setting; they fill the cylinder from the environment as a pump up to tank's max output pressure.
                    GasTankComponent? tankComp = null;
                    GasMixture? internalAir = null;
                    foreach (var ent in mechComp.ModuleContainer.ContainedEntities)
                    {
                        if (TryComp<MechGasCylinderModuleComponent>(ent, out _))
                        {
                            if (TryComp<GasTankComponent>(ent, out var t))
                            {
                                tankComp = t;
                                internalAir = t.Air;
                            }
                            break;
                        }
                    }

                    if (internalAir == null || tankComp == null)
                    {
                        fanModule.State = MechFanState.Off;
                        Dirty(fanModule.Owner, fanModule);
                    }
                    else
                    {
                        // Determine if external atmosphere is available
                        var external = _atmosphere.GetContainingMixture(uid);
                        var externalOk = external != null && external.Pressure > 0.05f; // allow small pressures too; we pump

                        // Target tank pressure ceiling: use tank's MaxOutputPressure as a safe cap
                        var targetTankPressure = tankComp.MaxOutputPressure;
                        var tankPressure = internalAir.Pressure;

                        // If already at/above target, idle
                        if (tankPressure >= targetTankPressure - 0.1f || !externalOk)
                        {
                            fanModule.State = MechFanState.Idle;
                            Dirty(fanModule.Owner, fanModule);
                        }
                        else
                        {
                            var energyConsumption = fanModule.EnergyConsumption * frameTime;
                            if (_mech.TryChangeEnergy(uid, -energyConsumption, mechComp))
                            {
                                fanModule.State = MechFanState.On;

                                if (external != null)
                                {
                                    // Compute moles needed to reach target pressure (limited by processing rate below)
                                    var desiredDeltaP = MathF.Max(0f, targetTankPressure - tankPressure);
                                    if (desiredDeltaP > 0)
                                    {
                                        var neededMoles = desiredDeltaP * internalAir.Volume / (internalAir.Temperature * Atmospherics.R);
                                        // Convert needed moles to volume from external based on its conditions
                                        var externalPressure = MathF.Max(external.Pressure, 0.01f);
                                        var extMolesPerLiter = externalPressure / (Atmospherics.R * external.Temperature);
                                        var intakeVolume = MathF.Min(fanModule.GasProcessingRate * frameTime, external.Volume);
                                        // Limit by external availability and our rate
                                        var molesAvailableAtRate = extMolesPerLiter * intakeVolume;
                                        var takeMoles = MathF.Min(neededMoles, molesAvailableAtRate);
                                        if (takeMoles > 0)
                                        {
                                            var removed = external.Remove(takeMoles);
                                            _atmosphere.Merge(internalAir, removed);
                                            uiDirty = true;
                                        }
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
                }
            }

            // Maintain cabin pressure from the cylinder when airtight
            if (mechComp.Airtight && TryComp<MechCabinPressureComponent>(uid, out var cabin))
            {
                var purgingActive = TryComp<MechCabinPurgeComponent>(uid, out var purgeComp) && purgeComp.CooldownRemaining > 0;
                if (!purgingActive && _mech.TryGetGasModuleAir(uid, out var tankAir) && tankAir != null)
                {
                    var cabinVolume = cabin.Air.Volume > 0 ? cabin.Air.Volume : Atmospherics.CellVolume;
                    var targetMoles = cabin.TargetPressure * cabinVolume / (Atmospherics.R * cabin.Air.Temperature);
                    var deficit = targetMoles - cabin.Air.TotalMoles;
                    if (deficit > 0f)
                    {
                        var removed = tankAir.Remove(deficit);
                        _atmosphere.Merge(cabin.Air, removed);
                        uiDirty = true;
                    }
                }
            }

            if (uiDirty)
                _mech.UpdateUserInterface(uid, mechComp);
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

        if (!TryComp(component.Mech, out MechCabinPressureComponent? cabin))
            return;

        args.Gas = cabin.Air;
        _mech.UpdateUserInterface(component.Mech, mech);
    }

    private void OnExhale(EntityUid uid, MechPilotComponent component, ExhaleLocationEvent args)
    {
        if (!TryComp<MechComponent>(component.Mech, out var mech))
            return;

        if (!mech.Airtight)
            return;

        if (!TryComp(component.Mech, out MechCabinPressureComponent? cabin))
            return;

        args.Gas = cabin.Air;
        _mech.UpdateUserInterface(component.Mech, mech);
    }

    private void OnExpose(EntityUid uid, MechPilotComponent component, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(component.Mech, out MechComponent? mech))
            return;

        if (mech.Airtight && TryComp(component.Mech, out MechCabinPressureComponent? cabin))
        {
            args.Handled = true;
            args.Gas = cabin.Air;
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

        // only OPEN (non-airtight) cabins mix with external atmosphere
        if (component.Airtight)
            return;

        if (TryComp(uid, out MechCabinPressureComponent? cabin))
            args.Air = cabin.Air;
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
