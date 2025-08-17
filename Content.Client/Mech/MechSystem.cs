using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Content.Client.UserInterface.Controls;
using System.Numerics;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Content.Client.Mech.Ui;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Client.Mech;

/// <inheritdoc/>
public sealed class MechSystem : SharedMechSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeLocalEvent<MechComponent, ComponentHandleState>(OnComponentHandleState);
        SubscribeLocalEvent<MechPilotComponent, PrepareMeleeLungeEvent>(OnPrepareMeleeLunge);
        SubscribeLocalEvent<MechComponent, PrepareMeleeLungeEvent>(OnPrepareMeleeLunge);
        SubscribeLocalEvent<MechPilotComponent, GetMeleeAttackerEntityEvent>(OnGetMeleeAttacker);
    }

    private void OnComponentHandleState(EntityUid uid, MechComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not MechComponentState state)
            return;

        if (TryComp<SpriteComponent>(uid, out var sprite))
        {
            UpdateMechSprite(uid, component, sprite);
        }
    }

    private void OnAppearanceChanged(EntityUid uid, MechComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        UpdateMechSprite(uid, component, args.Sprite);
    }

    private void UpdateMechSprite(EntityUid uid, MechComponent component, SpriteComponent sprite)
    {
        if (!_sprite.LayerExists((uid, sprite), MechVisualLayers.Base))
            return;

        var state = component.BaseState;
        var drawDepth = DrawDepth.Mobs;

        // Priority: Critical > Open > Base
        if (component.BrokenState != null && _appearance.TryGetData<bool>(uid, MechVisuals.Critical, out var critical) && critical)
        {
            state = component.BrokenState;
            drawDepth = DrawDepth.SmallMobs;
        }
        else if (component.OpenState != null && _appearance.TryGetData<bool>(uid, MechVisuals.Open, out var open) && open)
        {
            state = component.OpenState;
            drawDepth = DrawDepth.SmallMobs;
        }

        _sprite.LayerSetRsiState((uid, sprite), MechVisualLayers.Base, state);
        _sprite.SetDrawDepth((uid, sprite), (int)drawDepth);
    }

    private void OnPrepareMeleeLunge(EntityUid uid, MechPilotComponent comp, ref PrepareMeleeLungeEvent args)
    {
        args.SpawnAtMap = true;
        args.DisableTracking = true;
    }

    private void OnPrepareMeleeLunge(EntityUid uid, MechComponent comp, ref PrepareMeleeLungeEvent args)
    {
        args.SpawnAtMap = true;
        args.DisableTracking = true;
    }

    private void OnGetMeleeAttacker(EntityUid uid, MechPilotComponent comp, ref GetMeleeAttackerEntityEvent args)
    {
        if (args.Handled)
            return;

        args.Attacker = comp.Mech;
        args.Handled = true;
    }
}
