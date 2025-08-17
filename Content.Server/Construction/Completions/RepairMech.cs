using Content.Server.Mech.Systems;
using Content.Shared.Construction;
using Content.Shared.Mech.Components;
using JetBrains.Annotations;

namespace Content.Server.Construction.Completions;

/// <summary>
/// Repairs a mech that is in critical state, restoring it to normal operation.
/// </summary>
[UsedImplicitly, DataDefinition]
public sealed partial class RepairMech : IGraphAction
{
    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        if (!entityManager.TryGetComponent(uid, out MechComponent? mechComponent))
            return;

        var mechSys = entityManager.System<MechSystem>();

        mechSys.RepairMech(uid, mechComponent);
    }
}
