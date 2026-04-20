using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared.Speech.Muting;

public sealed class MutedSystem : EntitySystem
{
    public static readonly EntProtoId MutedEffect = "StatusEffectMuted";

    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<MutedComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<MutedComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(Entity<MutedComponent> ent, ref ComponentInit args)
    {
        _statusEffects.TrySetStatusEffectDuration(ent, MutedEffect);
    }

    private void OnComponentShutdown(Entity<MutedComponent> ent, ref ComponentShutdown args)
    {
        _statusEffects.TryRemoveStatusEffect(ent, MutedEffect);
    }
}
