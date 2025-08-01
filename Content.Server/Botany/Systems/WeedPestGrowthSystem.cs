using Content.Server.Botany.Components;
using Robust.Shared.Random;

namespace Content.Server.Botany.Systems;
public sealed class WeedPestGrowthSystem : PlantGrowthSystem
{
    // Default values for weed/pest growth
    private const float DefaultWeedGrowthChance = 0.01f;
    private const float DefaultWeedGrowthAmount = 0.5f;
    private const float DefaultPestDamageChance = 0.05f;
    private const float DefaultPestDamageAmount = 1f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantHolderComponent, OnPlantGrowEvent>(OnPlantGrow);
    }

    private void OnPlantGrow(EntityUid uid, PlantHolderComponent holder, OnPlantGrowEvent args)
    {
        if (holder.Seed == null || holder.Dead)
            return;

        TryComp<WeedPestGrowthComponent>(uid, out var weedPestComponent);

        var weedGrowthChance = weedPestComponent?.WeedGrowthChance ?? DefaultWeedGrowthChance;
        var weedGrowthAmount = weedPestComponent?.WeedGrowthAmount ?? DefaultWeedGrowthAmount;
        var pestDamageChance = weedPestComponent?.PestDamageChance ?? DefaultPestDamageChance;
        var pestDamageAmount = weedPestComponent?.PestDamageAmount ?? DefaultPestDamageAmount;

        // Weed growth logic
        if (_random.Prob(weedGrowthChance))
        {
            holder.WeedLevel += weedGrowthAmount;
            if (holder.DrawWarnings)
                holder.UpdateSpriteAfterUpdate = true;
        }

        // Pest damage logic
        if (_random.Prob(pestDamageChance))
        {
            holder.Health -= pestDamageAmount;
            if (holder.DrawWarnings)
                holder.UpdateSpriteAfterUpdate = true;
        }
    }
}
