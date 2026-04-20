using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;

namespace Content.Shared.Speech.EntitySystems;

/// <summary>
/// Base system for accents that should apply both directly and when relayed through status effects.
/// </summary>
public abstract class StatusEffectAccentSystem<TComponent> : EntitySystem
    where TComponent : Component
{
    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<TComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<TComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    /// <summary>
    /// Applies the accent transformation to the provided message.
    /// </summary>
    public string Accentuate(EntityUid uid, TComponent comp, string message)
    {
        return AccentuateInternal(uid, comp, message);
    }

    protected abstract string AccentuateInternal(EntityUid uid, TComponent comp, string message);

    private void OnAccent(Entity<TComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Entity, ent.Comp, args.Message);
    }

    private void OnAccentRelayed(Entity<TComponent> ent, ref StatusEffectRelayedEvent<AccentGetEvent> args)
    {
        args.Args.Message = Accentuate(args.Args.Entity, ent.Comp, args.Args.Message);
    }
}
