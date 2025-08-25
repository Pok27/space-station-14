using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Whitelist;

namespace Content.Server.Mech.Systems;

/// <summary>
/// Handles the insertion of mech module into mechs.
/// </summary>
public sealed class MechModuleSystem : MechInstallBaseSystem<MechModuleComponent, InsertModuleEvent>
{
    protected override IReadOnlyList<EntityUid> GetInstalled(MechComponent mech)
        => mech.ModuleContainer.ContainedEntities;

    protected override int GetInstalledCount(MechComponent mech) => mech.ModuleContainer.ContainedEntities.Count;
    protected override int GetMaxInstall(MechComponent mech) => mech.MaxModuleAmount;

    protected override bool IsWhitelistFail(MechComponent mech, EntityUid used)
        => EntityManager.System<EntityWhitelistSystem>().IsWhitelistFail(mech.ModuleWhitelist, used);

    protected override void PerformInsert(EntityUid mech, EntityUid item, MechComponent mechComp, MechModuleComponent itemComp)
    {
        EntityManager.System<SharedMechSystem>().InsertEquipment(mech, item, mechComp, moduleComponent: itemComp);
    }

    protected override float GetInstallDuration(EntityUid uid, MechModuleComponent comp) => comp.InstallDuration;
}
