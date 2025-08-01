using Content.Server.Atmos.EntitySystems;
using Content.Server.Botany.Components;
using Content.Shared.Atmos;

namespace Content.Server.Botany.Systems;
public sealed class AtmosphericGrowthSystem : PlantGrowthSystem
{
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly PlantHolderSystem _plantHolderSystem = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantHolderComponent, OnPlantGrowEvent>(OnPlantGrow);
    }

    private void OnPlantGrow(EntityUid uid, PlantHolderComponent holder, OnPlantGrowEvent args)
    {
        if (holder.Seed == null || holder.Dead)
            return;

        // Get atmospheric component or use default values
        AtmosphericGrowthComponent? atmosphericComponent = null;
        TryComp<AtmosphericGrowthComponent>(uid, out atmosphericComponent);

        var environment = _atmosphere.GetContainingMixture(uid, true, true) ?? GasMixture.SpaceGas;
        
        // Use component values or defaults
        var idealHeat = atmosphericComponent?.IdealHeat ?? 293f;
        var heatTolerance = atmosphericComponent?.HeatTolerance ?? 10f;
        var lowPressureTolerance = atmosphericComponent?.LowPressureTolerance ?? 81f;
        var highPressureTolerance = atmosphericComponent?.HighPressureTolerance ?? 121f;

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
