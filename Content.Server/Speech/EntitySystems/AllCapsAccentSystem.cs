using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Speech.EntitySystems;

/// <summary>
/// Applies the all-caps accent to speech and relayed speech status effect events.
/// </summary>
public sealed class AllCapsAccentSystem : EntitySystem
{
    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<AllCapsAccentComponent, AccentGetEvent>(OnAccent);
        SubscribeLocalEvent<AllCapsAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
    }

    /// <summary>
    /// Converts a speech message to uppercase using invariant casing rules.
    /// </summary>
    public string Accentuate(string message)
    {
        return message.ToUpperInvariant();
    }

    private void OnAccent(Entity<AllCapsAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message);
    }

    private void OnAccentRelayed(Entity<AllCapsAccentComponent> ent, ref StatusEffectRelayedEvent<AccentGetEvent> args)
    {
        args.Args.Message = Accentuate(args.Args.Message);
    }
}
