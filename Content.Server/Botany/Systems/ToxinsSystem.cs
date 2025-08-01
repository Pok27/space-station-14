using System;
using Content.Server.Botany.Components;

namespace Content.Server.Botany.Systems;

/// <summary>
/// Handles toxin tolerance and damage for plants.
/// </summary>
public sealed class ToxinsSystem : PlantGrowthSystem
{
    // Default values for toxins
    private const float DefaultToxinsTolerance = 4f;
    private const float DefaultToxinUptakeDivisor = 10f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantHolderComponent, OnPlantGrowEvent>(OnPlantGrow);
    }

    private void OnPlantGrow(EntityUid uid, PlantHolderComponent holder, OnPlantGrowEvent args)
    {
        if (holder.Seed == null || holder.Dead)
            return;

        TryComp<ToxinsComponent>(uid, out var toxinsComponent);

        var toxinsTolerance = toxinsComponent?.ToxinsTolerance ?? DefaultToxinsTolerance;
        var toxinUptakeDivisor = toxinsComponent?.ToxinUptakeDivisor ?? DefaultToxinUptakeDivisor;

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
