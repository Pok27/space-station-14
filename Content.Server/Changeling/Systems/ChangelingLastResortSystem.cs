using Content.Shared.Administration.Systems;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.Changeling.Systems;

public sealed partial class ChangelingLastResortSystem : SharedChangelingLastResortSystem
{
    private static readonly EntProtoId BaseMobLing = "BaseMobLing";

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

        TakeOverCorpse(args.User, target, mindId, mind);

        _popup.PopupEntity(Loc.GetString("changeling-takeover-success-self"), target, target, PopupType.Large);
    }

    private void TakeOverCorpse(EntityUid user, EntityUid target, EntityUid mindId, MindComponent mind)
    {
        // TODO: delete this after adding the stasis.
        _rejuvenate.PerformRejuvenate(target);
        _mind.TransferTo(mindId, target, mind: mind);

        if (ProtoMan.Resolve(BaseMobLing, out var lingBase))
            EntityManager.AddComponents(target, lingBase);

        QueueDel(user);
    }
}
