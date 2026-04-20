using Content.Server.Atmos.Components;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
/// Synchronizes <see cref="BarotraumaComponent.HasImmunity"/> with <see cref="PressureImmunityStatusEffectComponent"/>.
/// </summary>
public sealed class PressureImmunityStatusEffectSystem : EntitySystem
{
    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<PressureImmunityStatusEffectComponent, StatusEffectAppliedEvent>(OnStatusEffectApplied);
        SubscribeLocalEvent<PressureImmunityStatusEffectComponent, StatusEffectRemovedEvent>(OnStatusEffectRemoved);
    }

    private void OnStatusEffectApplied(Entity<PressureImmunityStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (TryComp<BarotraumaComponent>(args.Target, out var barotrauma))
            barotrauma.HasImmunity = true;
    }

    private void OnStatusEffectRemoved(Entity<PressureImmunityStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (TryComp<BarotraumaComponent>(args.Target, out var barotrauma))
            barotrauma.HasImmunity = false;
    }
}
