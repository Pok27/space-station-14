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
using Robust.Shared.GameStates;

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
        SubscribeLocalEvent<MechPilotComponent, PrepareMeleeLungeEvent>(OnPrepareMeleeLunge);
        SubscribeLocalEvent<MechComponent, PrepareMeleeLungeEvent>(OnPrepareMeleeLunge);
        SubscribeLocalEvent<MechPilotComponent, GetMeleeAttackerEntityEvent>(OnGetMeleeAttacker);
    }

    private void OnAppearanceChanged(EntityUid uid, MechComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_sprite.LayerExists((uid, args.Sprite), MechVisualLayers.Base))
            return;

        var state = component.BaseState;
        var drawDepth = DrawDepth.Mobs;

        // Priority: Broken > Open > Base
        if (component.BrokenState != null && _appearance.TryGetData<bool>(uid, MechVisuals.Broken, out var isBroken, args.Component) && isBroken)
        {
            state = component.BrokenState;
            drawDepth = DrawDepth.SmallMobs;
        }
        else if (component.OpenState != null && _appearance.TryGetData<bool>(uid, MechVisuals.Open, out var open, args.Component) && open)
        {
            state = component.OpenState;
            drawDepth = DrawDepth.SmallMobs;
        }

        _sprite.LayerSetRsiState((uid, args.Sprite), MechVisualLayers.Base, state);
        _sprite.SetDrawDepth((uid, args.Sprite), (int)drawDepth);
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
