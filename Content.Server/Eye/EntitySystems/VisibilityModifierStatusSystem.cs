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
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<VisibilityModifierStatusComponent, StatusEffectAppliedEvent>(OnStatusApplied);
        SubscribeLocalEvent<VisibilityModifierStatusComponent, StatusEffectRemovedEvent>(OnStatusRemoved);
    }

    private void OnStatusApplied(Entity<VisibilityModifierStatusComponent> ent, ref StatusEffectAppliedEvent args)
    {
        RefreshVisibilityModifiers((args.Target, null), skipEffect: ent.Owner);
    }

    private void OnStatusRemoved(Entity<VisibilityModifierStatusComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RefreshVisibilityModifiers((args.Target, null), extraEffect: ent.Comp);
    }

    /// <summary>
    /// Recomputes the entity's visibility layers from all active visibility-modifying status effects.
    /// </summary>
    public void RefreshVisibilityModifiers(
        Entity<VisibilityComponent?> ent,
        EntityUid? skipEffect = null,
        VisibilityModifierStatusComponent? extraEffect = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var currentModifiers = GetVisibilityModifiers(ent.Owner, skipEffect, extraEffect);
        var newModifiers = GetVisibilityModifiers(ent.Owner);

        var baseLayer = (ushort) ((ent.Comp.Layer & ~currentModifiers.AddLayers) | currentModifiers.RemoveLayers);
        var newLayer = (ushort) ((baseLayer & ~newModifiers.RemoveLayers) | newModifiers.AddLayers);
        if (newLayer == ent.Comp.Layer)
            return;

        _visibility.SetLayer(ent, newLayer, false);
        _visibility.RefreshVisibility(ent.Owner, ent.Comp);
    }

    /// <summary>
    /// Treats the entity's current visibility as the new baseline, then reapplies active status modifiers.
    /// </summary>
    public void CaptureCurrentVisibilityAsBaseline(
        Entity<VisibilityComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var modifiers = GetVisibilityModifiers(ent.Owner);
        var newLayer = (ushort) ((ent.Comp.Layer & ~modifiers.RemoveLayers) | modifiers.AddLayers);
        if (newLayer == ent.Comp.Layer)
            return;

        _visibility.SetLayer(ent, newLayer, false);
        _visibility.RefreshVisibility(ent.Owner, ent.Comp);
    }

    private VisibilityModifiers GetVisibilityModifiers(
        EntityUid uid,
        EntityUid? skipEffect = null,
        VisibilityModifierStatusComponent? extraEffect = null)
    {
        var modifiers = new VisibilityModifiers();

        foreach (var (effectUid, _, effect) in _statusEffects.EnumerateStatusEffects<VisibilityModifierStatusComponent>((uid, null)))
        {
            if (skipEffect != null && effectUid == skipEffect.Value)
                continue;

            modifiers.Include(effect);
        }

        if (extraEffect != null)
            modifiers.Include(extraEffect);

        return modifiers;
    }

    private struct VisibilityModifiers
    {
        public ushort AddLayers;
        public ushort RemoveLayers;

        public void Include(VisibilityModifierStatusComponent effect)
        {
            foreach (var layer in effect.AddVisibility)
            {
                AddLayers |= (ushort) layer;
            }

            foreach (var layer in effect.RemoveVisibility)
            {
                RemoveLayers |= (ushort) layer;
            }
        }
    }
}
