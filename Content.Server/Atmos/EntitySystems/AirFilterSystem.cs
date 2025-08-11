using Content.Server.Atmos.Components;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Mech.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Mech.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using System.Diagnostics.CodeAnalysis;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
/// Handles gas filtering and intake for <see cref="AirIntakeComponent"/> and <see cref="AirFilterComponent"/>.
/// </summary>
public sealed class AirFilterSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AirIntakeComponent, AtmosDeviceUpdateEvent>(OnIntakeUpdate);
        SubscribeLocalEvent<AirFilterComponent, AtmosDeviceUpdateEvent>(OnFilterUpdate);
    }

    private void OnIntakeUpdate(EntityUid uid, AirIntakeComponent intake, ref AtmosDeviceUpdateEvent args)
    {
        if (!GetAir(uid, out var air))
            return;

        // if the volume is filled there is nothing to do
        if (air.Pressure >= intake.Pressure)
            return;

        var environment = _atmosphere.GetContainingMixture(uid, args.Grid, args.Map, true, true);
        // nothing to intake from
        if (environment == null)
            return;

        // absolute maximum pressure change
        var pressureDelta = args.dt * intake.TargetPressureChange;
        pressureDelta = MathF.Min(pressureDelta, intake.Pressure - air.Pressure);
        if (pressureDelta <= 0)
            return;

        // how many moles to transfer to change internal pressure by pressureDelta
        // ignores temperature difference because lazy
        var transferMoles = pressureDelta * air.Volume / (environment.Temperature * Atmospherics.R);
        _atmosphere.Merge(air, environment.Remove(transferMoles));
    }

    private void OnFilterUpdate(EntityUid uid, AirFilterComponent filter, ref AtmosDeviceUpdateEvent args)
    {
        // if this filter is mounted inside a mech, only run when its fan module filter is enabled
        if (TryGetContainingMech(uid, out _, out var mech))
        {
            var fan = GetFanModule(mech);
            if (fan == null || !fan.FilterEnabled)
                return;
        }

        if (!GetAir(uid, out var air))
            return;

        var ratio = MathF.Min(1f, args.dt * filter.TransferRate * _atmosphere.PumpSpeedup());
        var removed = air.RemoveRatio(ratio);
        // nothing left to remove from the volume
        if (MathHelper.CloseToPercent(removed.TotalMoles, 0f))
            return;

        // when oxygen gets too low start removing overflow gases (nitrogen) to maintain oxygen ratio
        var oxygen = air.GetMoles(filter.Oxygen) / air.TotalMoles;
        var gases = oxygen >= filter.TargetOxygen ? filter.Gases : filter.OverflowGases;

        GasMixture? destination = null;
        if (args.Grid is {} grid)
        {
            var position = _transform.GetGridTilePositionOrDefault(uid);
            destination = _atmosphere.GetTileMixture(grid, args.Map, position, true);
        }

        if (destination != null)
        {
            _atmosphere.ScrubInto(removed, destination, gases);
        }
        else
        {
            // filtering into space/planet so just discard them
            foreach (var gas in gases)
            {
                removed.SetMoles(gas, 0f);
            }
        }

        _atmosphere.Merge(air, removed);
    }

    /// <summary>
    /// Uses <see cref="GetFilterAirEvent"/> to get an internal volume of air on an entity.
    /// Used for both filter and intake.
    /// </summary>
    public bool GetAir(EntityUid uid, [NotNullWhen(true)] out GasMixture? air)
    {
        air = null;

        var ev = new GetFilterAirEvent();
        RaiseLocalEvent(uid, ref ev);
        air = ev.Air;

        if (air == null && TryGetContainingMech(uid, out _, out var mech))
        {
            if (TryGetGasModuleAir(mech, out var tankAir))
                air = tankAir;
        }

        return air != null;
    }

    private bool TryGetContainingMech(EntityUid uid, [NotNullWhen(true)] out EntityUid mechUid, [NotNullWhen(true)] out MechComponent mech)
    {
        mechUid = EntityUid.Invalid;
        mech = default!;

        var current = uid;
        for (var i = 0; i < 5; i++)
        {
            if (_containers.TryGetContainingContainer(current, out var container))
            {
                var owner = container.Owner;
                if (TryComp(owner, out MechComponent? comp))
                {
                    mech = comp!;
                    mechUid = owner;
                    return true;
                }
                current = owner;
                continue;
            }
            break;
        }
        return false;
    }

    private bool TryGetGasModuleAir(MechComponent mech, out GasMixture? air)
    {
        air = null;
        if (mech.ModuleContainer == null)
            return false;

        foreach (var ent in mech.ModuleContainer.ContainedEntities)
        {
            if (TryComp<GasTankComponent>(ent, out var tank))
            {
                air = tank.Air;
                if (air != null)
                    return true;
            }
        }
        return false;
    }

    private MechFanModuleComponent? GetFanModule(MechComponent mech)
    {
        foreach (var ent in mech.ModuleContainer.ContainedEntities)
        {
            if (TryComp<MechFanModuleComponent>(ent, out var fan))
                return fan;
        }
        return null;
    }
}

/// <summary>
/// Get a reference to an entity's air volume to filter.
/// Do not create a new mixture as this will be modified when filtering and intaking air.
/// </summary>
[ByRefEvent]
public record struct GetFilterAirEvent(GasMixture? Air = null);
