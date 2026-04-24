using Content.Server.GameTicking;
using Content.Shared.Eye;
using Content.Shared.Revenant.Components;
using Content.Shared.Revenant.EntitySystems;
using Content.Shared.StatusEffectNew;
using Robust.Server.GameObjects;

namespace Content.Server.Revenant.EntitySystems;

public sealed class CorporealSystem : SharedCorporealSystem
{
    [Dependency] private readonly VisibilitySystem _visibilitySystem = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    public override void OnApplied(Entity<CorporealStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        base.OnApplied(ent, ref args);

        if (TryComp<VisibilityComponent>(args.Target, out var visibility))
        {
            _visibilitySystem.RemoveLayer((args.Target, visibility), (int) VisibilityFlags.Ghost, false);
            _visibilitySystem.AddLayer((args.Target, visibility), (int) VisibilityFlags.Normal, false);
            _visibilitySystem.RefreshVisibility(args.Target, visibility);
        }
    }

    public override void OnRemoved(Entity<CorporealStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        base.OnRemoved(ent, ref args);

        if (TryComp<VisibilityComponent>(args.Target, out var visibility) && _ticker.RunLevel != GameRunLevel.PostRound)
        {
            _visibilitySystem.AddLayer((args.Target, visibility), (int) VisibilityFlags.Ghost, false);
            _visibilitySystem.RemoveLayer((args.Target, visibility), (int) VisibilityFlags.Normal, false);
            _visibilitySystem.RefreshVisibility(args.Target, visibility);
        }
    }
}
