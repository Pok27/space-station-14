using System;
using Content.Server.Botany.Components;

namespace Content.Server.Botany.Systems;

/// <summary>
/// Handles toxin tolerance and damage for plants.
/// </summary>
public sealed class ToxinsSystem : PlantGrowthSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantHolderComponent, OnPlantGrowEvent>(OnPlantGrow);
    }

    private void OnPlantGrow(EntityUid uid, PlantHolderComponent holder, OnPlantGrowEvent args)
    {
        if (holder.Seed == null || holder.Dead)
            return;

        // Get toxins component or use default values
        ToxinsComponent? toxinsComponent = null;
        TryComp<ToxinsComponent>(uid, out toxinsComponent);

        // Use component values or defaults
        var toxinsTolerance = toxinsComponent?.ToxinsTolerance ?? 4f;
        var toxinUptakeDivisor = toxinsComponent?.ToxinUptakeDivisor ?? 10f;

        if (holder.Toxins > 0)
        {
            var toxinUptake = MathF.Max(1, MathF.Round(holder.Toxins / toxinUptakeDivisor));
            if (holder.Toxins > toxinsTolerance)
            {
                holder.Health -= toxinUptake;
            }

            holder.Toxins -= toxinUptake;
            if (holder.DrawWarnings)
                holder.UpdateSpriteAfterUpdate = true;
        }
    }
}
