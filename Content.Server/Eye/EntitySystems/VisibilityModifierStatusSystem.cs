using Content.Server.Eye.Components;
using Content.Shared.Eye;
using Content.Shared.StatusEffectNew;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Server.Eye.EntitySystems;

/// <summary>
/// Refreshes visibility layer modifiers contributed by active status effects.
/// </summary>
public sealed class VisibilityModifierStatusSystem : EntitySystem
{
    [Dependency] private readonly VisibilitySystem _visibility = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<VisibilityModifierStatusComponent, StatusEffectAppliedEvent>(OnStatusApplied);
        SubscribeLocalEvent<VisibilityModifierStatusComponent, StatusEffectRemovedEvent>(OnStatusRemoved);
        SubscribeLocalEvent<VisibilityModifierStatusComponent, StatusEffectRelayedEvent<RefreshVisibilityModifiersEvent>>(OnRefreshVisibilityModifiers);
    }

    private void OnStatusApplied(Entity<VisibilityModifierStatusComponent> ent, ref StatusEffectAppliedEvent args)
    {
        RefreshVisibilityModifiers((args.Target, null));
    }

    private void OnStatusRemoved(Entity<VisibilityModifierStatusComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RefreshVisibilityModifiers((args.Target, null));
    }

    private void OnRefreshVisibilityModifiers(
        Entity<VisibilityModifierStatusComponent> ent,
        ref StatusEffectRelayedEvent<RefreshVisibilityModifiersEvent> args)
    {
        var ev = args.Args;
        ev.ModifierCount++;

        foreach (var layer in ent.Comp.AddVisibility)
        {
            ev.AddLayer(layer);
        }

        foreach (var layer in ent.Comp.RemoveVisibility)
        {
            ev.RemoveLayer(layer);
        }

        args.Args = ev;
    }

    /// <summary>
    /// Recomputes the entity's visibility layers from all active visibility-modifying status effects.
    /// </summary>
    public void RefreshVisibilityModifiers(
        Entity<VisibilityComponent?> ent,
        VisibilityModifierStatusTrackerComponent? tracker = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var ev = new RefreshVisibilityModifiersEvent();
        RaiseLocalEvent(ent, ref ev);

        if (ev.ModifierCount == 0)
        {
            if (!Resolve(ent.Owner, ref tracker, false))
                return;

            var baseLayer = GetBaseLayer(ent.Comp.Layer, tracker);
            tracker.LastAddedLayers = 0;
            tracker.LastRemovedLayers = 0;

            if (ent.Comp.Layer != baseLayer)
            {
                _visibility.SetLayer(ent, baseLayer, false);
                _visibility.RefreshVisibility(ent.Owner, ent.Comp);
            }

            RemComp<VisibilityModifierStatusTrackerComponent>(ent.Owner);
            return;
        }

        tracker ??= EnsureComp<VisibilityModifierStatusTrackerComponent>(ent.Owner);

        var newLayer = GetBaseLayer(ent.Comp.Layer, tracker);
        newLayer = (ushort) ((newLayer & ~ev.RemoveLayers) | ev.AddLayers);

        tracker.LastAddedLayers = ev.AddLayers;
        tracker.LastRemovedLayers = ev.RemoveLayers;

        if (ent.Comp.Layer == newLayer)
            return;

        _visibility.SetLayer(ent, newLayer, false);
        _visibility.RefreshVisibility(ent.Owner, ent.Comp);
    }

    /// <summary>
    /// Resets the tracked modifier delta so the entity's current visibility becomes the new baseline.
    /// </summary>
    public void CaptureCurrentVisibilityAsBaseline(
        Entity<VisibilityComponent?> ent,
        VisibilityModifierStatusTrackerComponent? tracker = null)
    {
        if (!Resolve(ent.Owner, ref tracker, false))
            return;

        tracker.LastAddedLayers = 0;
        tracker.LastRemovedLayers = 0;
    }

    private static ushort GetBaseLayer(ushort currentLayer, VisibilityModifierStatusTrackerComponent tracker)
    {
        return (ushort) ((currentLayer & ~tracker.LastAddedLayers) | tracker.LastRemovedLayers);
    }
}
