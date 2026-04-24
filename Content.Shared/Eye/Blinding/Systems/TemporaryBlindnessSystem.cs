using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Flash;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared.Eye.Blinding.Systems;

public sealed class TemporaryBlindnessSystem : EntitySystem
{
    public static readonly EntProtoId BlindingStatusEffect = "StatusEffectTemporaryBlindness";

    [Dependency] private readonly BlindableSystem _blindableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TemporaryBlindnessStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<TemporaryBlindnessStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<TemporaryBlindnessStatusEffectComponent, StatusEffectRelayedEvent<CanSeeAttemptEvent>>(OnBlindTrySee);
        SubscribeLocalEvent<TemporaryBlindnessStatusEffectComponent, StatusEffectRelayedEvent<FlashAttemptEvent>>(OnFlashAttempt);
    }

    private void OnApplied(Entity<TemporaryBlindnessStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _blindableSystem.UpdateIsBlind(args.Target);
    }

    private void OnRemoved(Entity<TemporaryBlindnessStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _blindableSystem.UpdateIsBlind(args.Target);
    }

    private void OnBlindTrySee(Entity<TemporaryBlindnessStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CanSeeAttemptEvent> args)
    {
        var ev = args.Args;
        ev.Cancel();
        args.Args = ev;
    }

    private void OnFlashAttempt(Entity<TemporaryBlindnessStatusEffectComponent> ent, ref StatusEffectRelayedEvent<FlashAttemptEvent> args)
    {
        var ev = args.Args;
        ev.Cancelled = true;
        args.Args = ev;
    }
}
