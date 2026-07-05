using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.Administration.Systems;
using Content.Shared.Antag;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Changeling.Systems;

public sealed partial class ChangelingLastResortSystem : SharedChangelingLastResortSystem
{
    private static readonly EntProtoId ChangelingRule = "Changeling";
    private static readonly ProtoId<AntagSpecifierPrototype> ChangelingAntag = "Changeling";

    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private RejuvenateSystem _rejuvenate = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [SubscribeLocalEvent]
    private void OnTakeOverCorpseDoAfter(Entity<ChangelingSlugComponent> ent,
        ref ChangelingTakeOverCorpseDoAfterEvent args)
    {
        args.Handled = true;

        if (args.Cancelled || args.Target is not { } target || !CanTakeOver(args.User, target, showPopups: false))
            return;

        if (!_mind.TryGetMind(args.User, out var mindId, out var mind))
            return;

        // TODO: delete this after adding the stasis.
        _rejuvenate.PerformRejuvenate(target);
        _mind.TransferTo(mindId, target, mind: mind);
        TakeOverCorpse(target, mind);
        PredictedQueueDel(args.User);

        _popup.PopupEntity(Loc.GetString("changeling-takeover-success-self"), target, target, PopupType.Large);
    }

    private void TakeOverCorpse(EntityUid target, MindComponent mind)
    {
        if (mind.UserId is { } userId && _player.TryGetSessionById(userId, out var session))
        {
            _antag.TryReapplyAntagConfiguration<ChangelingRuleComponent>(session,
                target,
                ChangelingRule,
                ChangelingAntag);
        }
    }
}
