using Content.Shared.StatusEffectNew;

namespace Content.Shared.Speech.Muting;

public sealed class MutedSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<MutedComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<MutedComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(Entity<MutedComponent> ent, ref ComponentInit args)
    {
        _statusEffects.TrySetStatusEffectDuration(ent, MutedStatusEffectComponent.StatusEffectPrototype);
    }

    private void OnComponentShutdown(Entity<MutedComponent> ent, ref ComponentShutdown args)
    {
        _statusEffects.TryRemoveStatusEffect(ent, MutedStatusEffectComponent.StatusEffectPrototype);
    }
}
