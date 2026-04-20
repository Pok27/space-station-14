using Content.Server.Popups;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Abilities.Mime;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Puppet;
using Content.Shared.Speech;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Speech.Muting;

/// <summary>
/// Handles the speech restrictions imposed by <see cref="MutedStatusEffectComponent"/>.
/// </summary>
public sealed class MutedStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<MutedStatusEffectComponent, StatusEffectRelayedEvent<SpeakAttemptEvent>>(OnSpeakAttempt);
        SubscribeLocalEvent<MutedStatusEffectComponent, StatusEffectRelayedEvent<EmoteEvent>>(OnEmote, before: new[] { typeof(VocalSystem), typeof(MumbleAccentSystem) });
        SubscribeLocalEvent<MutedStatusEffectComponent, StatusEffectRelayedEvent<ScreamActionEvent>>(OnScreamAction, before: new[] { typeof(VocalSystem) });
    }

    private void OnEmote(EntityUid uid, MutedStatusEffectComponent component, ref StatusEffectRelayedEvent<EmoteEvent> args)
    {
        if (args.Args.Handled)
            return;

        // Still leaves the text so it looks like they are pantomiming a laugh.
        if (args.Args.Emote.Category.HasFlag(EmoteCategory.Vocal))
        {
            var ev = args.Args;
            ev.Handled = true;
            args.Args = ev;
        }
    }

    private void OnScreamAction(EntityUid uid, MutedStatusEffectComponent component, ref StatusEffectRelayedEvent<ScreamActionEvent> args)
    {
        if (args.Args.Handled)
            return;

        if (HasComp<MimePowersComponent>(uid))
            _popupSystem.PopupEntity(Loc.GetString("mime-cant-speak"), uid, uid);
        else
            _popupSystem.PopupEntity(Loc.GetString("speech-muted"), uid, uid);

        args.Args.Handled = true;
    }

    private void OnSpeakAttempt(EntityUid uid, MutedStatusEffectComponent component, ref StatusEffectRelayedEvent<SpeakAttemptEvent> args)
    {
        if (HasComp<MimePowersComponent>(uid))
            _popupSystem.PopupEntity(Loc.GetString("mime-cant-speak"), uid, uid);
        else if (HasComp<VentriloquistPuppetComponent>(uid))
            _popupSystem.PopupEntity(Loc.GetString("ventriloquist-puppet-cant-speak"), uid, uid);
        else
            _popupSystem.PopupEntity(Loc.GetString("speech-muted"), uid, uid);

        args.Args.Cancel();
    }
}
