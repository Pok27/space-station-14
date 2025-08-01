using Content.Server.Atmos.EntitySystems;
using Content.Server.Botany.Components;
using Content.Shared.Atmos;

namespace Content.Server.Botany.Systems;
public sealed class AtmosphericGrowthSystem : PlantGrowthSystem
{
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolderSystem = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    // Default values for atmospheric growth
    private const float DefaultIdealHeat = 293f;
    private const float DefaultHeatTolerance = 10f;
    private const float DefaultLowPressureTolerance = 81f;
    private const float DefaultHighPressureTolerance = 121f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantHolderComponent, OnPlantGrowEvent>(OnPlantGrow);
    }

    private void OnPlantGrow(EntityUid uid, PlantHolderComponent holder, OnPlantGrowEvent args)
    {
        if (holder.Seed == null || holder.Dead)
            return;

        TryComp<AtmosphericGrowthComponent>(uid, out var atmosphericComponent);

        var environment = _atmosphere.GetContainingMixture(uid, true, true) ?? GasMixture.SpaceGas;
        
        var idealHeat = atmosphericComponent?.IdealHeat ?? DefaultIdealHeat;
        var heatTolerance = atmosphericComponent?.HeatTolerance ?? DefaultHeatTolerance;
        var lowPressureTolerance = atmosphericComponent?.LowPressureTolerance ?? DefaultLowPressureTolerance;
        var highPressureTolerance = atmosphericComponent?.HighPressureTolerance ?? DefaultHighPressureTolerance;

        if (MathF.Abs(environment.Temperature - idealHeat) > heatTolerance)
        {
            holder.Health -= _random.Next(1, 3);
            holder.ImproperHeat = true;
            if (holder.DrawWarnings)
                holder.UpdateSpriteAfterUpdate = true;
        }
        else
        {
            holder.ImproperHeat = false;
        }

        var pressure = environment.Pressure;
        if (pressure < lowPressureTolerance || pressure > highPressureTolerance)
        {
            holder.Health -= _random.Next(1, 3);
            holder.ImproperPressure = true;
            if (holder.DrawWarnings)
                holder.UpdateSpriteAfterUpdate = true;
        }
        else
        {
            holder.ImproperPressure = false;
        }
    }
}
