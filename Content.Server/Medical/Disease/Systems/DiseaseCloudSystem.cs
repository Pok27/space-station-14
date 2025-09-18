using System;
using System.Collections.Generic;
using Content.Shared.Medical.Disease;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Disease;

/// <summary>
/// Transient airborne or residue infection area. Periodically attempts to infect entities in range.
/// </summary>
public sealed class DiseaseCloudSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly DiseaseSystem _disease = default!;
    [Dependency] private readonly Robust.Shared.Prototypes.IPrototypeManager _prototypes = default!;

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DiseaseCloudComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var cloud, out var xform))
        {
            if (cloud.Expiry <= now)
            {
                QueueDel(uid);
                continue;
            }

            if (cloud.NextTick > now)
                continue;

            cloud.NextTick = now + cloud.TickInterval;

            var mapPos = _xform.GetMapCoordinates(uid, xform);
            if (mapPos.MapId == MapId.Nullspace)
                continue;

            var ents = _lookup.GetEntitiesInRange(mapPos, cloud.Range, LookupFlags.Dynamic | LookupFlags.Sundries);
            foreach (var ent in ents)
            {
                foreach (var diseaseId in cloud.Diseases)
                {
                    if (_prototypes.TryIndex<DiseasePrototype>(diseaseId, out var proto))
                    {
                        // Clouds represent transient airborne spread; gate by Airborne flag.
                        if (!proto.SpreadFlags.Contains(DiseaseSpreadFlags.Airborne))
                            continue;

                        _disease.TryInfectWithChance(ent, diseaseId, proto.AirborneInfect, 1);
                    }
                }
            }
        }
    }
}
