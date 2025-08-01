using Content.Server.Botany.Components;
using Robust.Shared.Random;

namespace Content.Server.Botany.Systems;
public sealed class WeedPestGrowthSystem : PlantGrowthSystem
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

        // Get weed/pest component or use default values
        WeedPestGrowthComponent? weedPestComponent = null;
        TryComp<WeedPestGrowthComponent>(uid, out weedPestComponent);

        // Use component values or defaults
        var weedGrowthChance = weedPestComponent?.WeedGrowthChance ?? 0.01f;
        var weedGrowthAmount = weedPestComponent?.WeedGrowthAmount ?? 0.5f;
        var pestDamageChance = weedPestComponent?.PestDamageChance ?? 0.05f;
        var pestDamageAmount = weedPestComponent?.PestDamageAmount ?? 1f;

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
