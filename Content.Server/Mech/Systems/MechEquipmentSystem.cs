using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Whitelist;
using Robust.Shared.GameObjects;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Handles the insertion of mech equipment into mechs.
/// </summary>
public sealed class MechEquipmentSystem : EntitySystem
{
    [Dependency] private readonly MechSystem _mech = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MechEquipmentComponent, AfterInteractEvent>(OnUsed);
        SubscribeLocalEvent<MechEquipmentComponent, InsertEquipmentEvent>(OnInsertEquipment);
    }

    private void OnUsed(EntityUid uid, MechEquipmentComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        var mech = args.Target.Value;
        if (!TryComp<MechComponent>(mech, out var mechComp))
            return;

        if (mechComp.Broken)
            return;

        // Block install if pilot inside
        if (mechComp.PilotSlot.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("mech-install-blocked-pilot", ("item", uid)), args.User);
            return;
        }

        if (args.User == mechComp.PilotSlot.ContainedEntity)
            return;

        // Duplicate by prototype id
        if (TryComp<MetaDataComponent>(uid, out var md) && md.EntityPrototype != null)
        {
            var id = md.EntityPrototype.ID;
            foreach (var ent in mechComp.EquipmentContainer.ContainedEntities)
            {
                if (TryComp<MetaDataComponent>(ent, out var md2) && md2.EntityPrototype != null && md2.EntityPrototype.ID == id)
                {
                    _popup.PopupEntity(Loc.GetString("mech-duplicate-equipment"), args.User);
                    return;
                }
            }
        }

        if (mechComp.EquipmentContainer.ContainedEntities.Count >= mechComp.MaxEquipmentAmount)
        {
            _popup.PopupEntity(Loc.GetString("mech-capacity-equipment-full"), args.User);
            return;
        }

        if (_whitelistSystem.IsWhitelistFail(mechComp.EquipmentWhitelist, args.Used))
            return;

        _popup.PopupEntity(Loc.GetString("mech-equipment-begin-install", ("item", uid)), mech);

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.InstallDuration, new InsertEquipmentEvent(), uid, target: mech, used: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnInsertEquipment(EntityUid uid, MechEquipmentComponent component, InsertEquipmentEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        _popup.PopupEntity(Loc.GetString("mech-equipment-finish-install", ("item", uid)), args.Args.Target.Value);
        _mech.InsertEquipment(args.Args.Target.Value, uid);

        args.Handled = true;
    }
}
