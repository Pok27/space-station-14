using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Shared.Actions;
using Content.Shared.Administration.Systems;
using Content.Shared.Antag;
using Content.Shared.Changeling.Components;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Changeling.Systems;

public sealed class ChangelingLastResortSystem : EntitySystem
{
    private static readonly EntProtoId ChangelingRule = "Changeling";
    private static readonly ProtoId<AntagSpecifierPrototype> ChangelingAntag = "Changeling";

    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChangelingLastResortAbilityComponent, ChangelingLastResortActionEvent>(OnLastResortAction);
        SubscribeLocalEvent<ChangelingSlugComponent, MapInitEvent>(OnTakeOverMapInit);
        SubscribeLocalEvent<ChangelingSlugComponent, ComponentShutdown>(OnTakeOverShutdown);
        SubscribeLocalEvent<ChangelingSlugComponent, ChangelingTakeOverCorpseActionEvent>(OnTakeOverCorpseAction);
        SubscribeLocalEvent<ChangelingSlugComponent, ChangelingTakeOverCorpseDoAfterEvent>(OnTakeOverCorpseDoAfter);
    }

    private void OnTakeOverMapInit(Entity<ChangelingSlugComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnTakeOverShutdown(Entity<ChangelingSlugComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ActionEntity != null)
            _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnLastResortAction(Entity<ChangelingLastResortAbilityComponent> ent, ref ChangelingLastResortActionEvent args)
    {
        if (args.Handled || !_mind.TryGetMind(args.Performer, out var mindId, out var mind))
            return;

        args.Handled = true;

        var slug = Spawn(ent.Comp.SlugPrototype, _transform.GetMoverCoordinates(args.Performer));
        _mind.TransferTo(mindId, slug, mind: mind);
        _audio.PlayPvs(ent.Comp.Sound, args.Performer);
        _gibbing.Gib(args.Performer);
    }

    private void OnTakeOverCorpseAction(Entity<ChangelingSlugComponent> ent, ref ChangelingTakeOverCorpseActionEvent args)
    {
        if (args.Handled || !CanTakeOver(args.Target))
            return;

        args.Handled = true;

        _audio.PlayPvs(ent.Comp.Sound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("changeling-takeover-start-self"), ent.Owner, ent.Owner, PopupType.Medium);
        _popup.PopupEntity(
            Loc.GetString("changeling-takeover-start-others", ("user", ent.Owner)),
            ent.Owner,
            Filter.PvsExcept(ent.Owner),
            true,
            PopupType.MediumCaution);

        var doAfter = new DoAfterArgs(
            EntityManager,
            ent.Owner,
            ent.Comp.TakeOverDuration,
            new ChangelingTakeOverCorpseDoAfterEvent(),
            ent,
            target: args.Target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.None,
            NeedHand = false,
            RequireCanInteract = false,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnTakeOverCorpseDoAfter(Entity<ChangelingSlugComponent> ent, ref ChangelingTakeOverCorpseDoAfterEvent args)
    {
        args.Handled = true;

        if (args.Cancelled || args.Target is not { } target || !CanTakeOver(target))
            return;

        var performer = args.User;
        if (!_mind.TryGetMind(performer, out var mindId, out var mind))
            return;

        _rejuvenate.PerformRejuvenate(target);
        _mind.TransferTo(mindId, target, mind: mind);

        if (mind.UserId is { } userId && _player.TryGetSessionById(userId, out var session))
            _antag.TryApplyAntagConfiguration<ChangelingRuleComponent>(session, target, ChangelingRule, ChangelingAntag);

        QueueDel(performer);

        _popup.PopupEntity(Loc.GetString("changeling-takeover-success-self"), target, target, PopupType.Large);
    }

    private bool CanTakeOver(EntityUid target)
    {
        if (!HasComp<HumanoidProfileComponent>(target))
            return false;

        if (_mobState.IsDead(target))
            return true;

        _popup.PopupEntity(Loc.GetString("changeling-takeover-not-dead"), target, target, PopupType.SmallCaution);
        return false;
    }
}
