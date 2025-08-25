using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Whitelist;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Handles the insertion of mech equipment into mechs.
/// </summary>
public sealed class MechEquipmentSystem : MechInstallBaseSystem<MechEquipmentComponent, InsertEquipmentEvent>
{
    protected override IReadOnlyList<EntityUid> GetInstalled(MechComponent mech)
        => mech.EquipmentContainer.ContainedEntities;

    protected override int GetInstalledCount(MechComponent mech) => mech.EquipmentContainer.ContainedEntities.Count;
    protected override int GetMaxInstall(MechComponent mech) => mech.MaxEquipmentAmount;

    protected override bool IsWhitelistFail(MechComponent mech, EntityUid used)
        => EntityManager.System<EntityWhitelistSystem>().IsWhitelistFail(mech.EquipmentWhitelist, used);

    protected override void PerformInsert(EntityUid mech, EntityUid item, MechComponent mechComp, MechEquipmentComponent itemComp)
    {
        EntityManager.System<SharedMechSystem>().InsertEquipment(mech, item, mechComp, equipmentComponent: itemComp);
    }

    protected override float GetInstallDuration(EntityUid uid, MechEquipmentComponent comp) => comp.InstallDuration;
}
