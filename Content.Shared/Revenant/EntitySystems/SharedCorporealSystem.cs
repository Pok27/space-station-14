using Content.Shared.Physics;
using Robust.Shared.Physics;
using System.Linq;
using Content.Shared.Movement.Systems;
using Content.Shared.Revenant.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.Revenant.EntitySystems;

/// <summary>
/// Makes the revenant solid when the component is applied.
/// Additionally applies a few visual effects.
/// Used for status effect.
/// </summary>
public abstract class SharedCorporealSystem : EntitySystem
{
    public static readonly EntProtoId CorporealStatusEffect = "StatusEffectCorporeal";

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CorporealStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<CorporealStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<CorporealStatusEffectComponent, StatusEffectRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefresh);
    }

    private void OnRefresh(
        Entity<CorporealStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        args.Args.ModifySpeed(ent.Comp.MovementSpeedDebuff, ent.Comp.MovementSpeedDebuff);
    }

    public virtual void OnApplied(Entity<CorporealStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _appearance.SetData(args.Target, RevenantVisuals.Corporeal, true);

        if (TryComp<FixturesComponent>(args.Target, out var fixtures) && fixtures.FixtureCount >= 1)
        {
            var fixture = fixtures.Fixtures.First();

            _physics.SetCollisionMask(args.Target, fixture.Key, fixture.Value, (int) (CollisionGroup.SmallMobMask | CollisionGroup.GhostImpassable), fixtures);
            _physics.SetCollisionLayer(args.Target, fixture.Key, fixture.Value, (int) CollisionGroup.SmallMobLayer, fixtures);
        }
        _movement.RefreshMovementSpeedModifiers(args.Target);
    }

    public virtual void OnRemoved(Entity<CorporealStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _appearance.SetData(args.Target, RevenantVisuals.Corporeal, false);

        if (TryComp<FixturesComponent>(args.Target, out var fixtures) && fixtures.FixtureCount >= 1)
        {
            var fixture = fixtures.Fixtures.First();

            _physics.SetCollisionMask(args.Target, fixture.Key, fixture.Value, (int) CollisionGroup.GhostImpassable, fixtures);
            _physics.SetCollisionLayer(args.Target, fixture.Key, fixture.Value, 0, fixtures);
        }

        _movement.RefreshMovementSpeedModifiers(args.Target);
    }
}
