using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;

namespace Content.Shared.Speech.EntitySystems;

/// <summary>
/// Base system for accents that should apply both directly and when relayed through other entities.
/// </summary>
public abstract class RelayAccentSystem<T> : EntitySystem where T : Component
{
    /// <summary>
    /// Systems this accent should run before for direct speech accenting.
    /// </summary>
    protected virtual Type[]? AccentBefore => null;

    /// <summary>
    /// Systems this accent should run after for direct speech accenting.
    /// </summary>
    protected virtual Type[]? AccentAfter => null;

    /// <summary>
    /// Systems this accent should run before for relayed speech accenting.
    /// </summary>
    protected virtual Type[]? RelayAccentBefore => AccentBefore;

    /// <summary>
    /// Systems this accent should run after for relayed speech accenting.
    /// </summary>
    protected virtual Type[]? RelayAccentAfter => AccentAfter;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<T, AccentGetEvent>(OnAccent, before: AccentBefore, after: AccentAfter);
        SubscribeLocalEvent<T, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed, before: RelayAccentBefore, after: RelayAccentAfter);
    }

    /// <summary>
    /// Applies the accent transformation to the provided message.
    /// </summary>
    private string Accentuate(EntityUid uid, T comp, string message)
    {
        return AccentuateInternal(uid, comp, message);
    }

    protected abstract string AccentuateInternal(EntityUid uid, T comp, string message);

    private void OnAccent(Entity<T> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Entity, ent.Comp, args.Message);
    }

    private void OnAccentRelayed(Entity<T> ent, ref StatusEffectRelayedEvent<AccentGetEvent> args)
    {
        args.Args.Message = Accentuate(args.Args.Entity, ent.Comp, args.Args.Message);
    }
}
