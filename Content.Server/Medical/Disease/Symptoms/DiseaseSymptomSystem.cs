using System;
using Content.Shared.Medical.Disease;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using Content.Shared.Mobs.Systems;

namespace Content.Server.Medical.Disease;

/// <summary>
/// Encapsulates symptom-side effects and secondary spread mechanics for diseases.
/// </summary>
public sealed partial class DiseaseSymptomSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    /// <inheritdoc/>
    /// <summary>
    /// Executes the side-effects for a triggered symptom on a carrier.
    /// </summary>
    public void TriggerSymptom(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease, DiseaseSymptomPrototype symptom)
    {
        // Skip this symptom when the carrier is dead.
        if (symptom.OnlyWhenAlive && _mobState.IsDead(ent.Owner))
            return;

        var deps = _entitySystemManager.DependencyCollection;

        if (symptom.SingleBehavior && symptom.Behaviors.Count > 0)
        {
            // Run exactly one random behavior.
            var idx = _random.Next(0, symptom.Behaviors.Count);
            var behavior = symptom.Behaviors[idx];
            deps.InjectDependencies(behavior, oneOff: true);
            behavior.OnSymptom(ent.Owner, disease);
        }
        else
        {
            foreach (var behavior in symptom.Behaviors)
            {
                deps.InjectDependencies(behavior, oneOff: true);
                behavior.OnSymptom(ent.Owner, disease);
            }
        }

        // Apply configurable effects for any symptom. If not configured in YAML, these are no-ops.
        ApplyCloud(symptom, ent, disease);
        LeaveResidue(symptom, ent, disease);
    }

    /// <summary>
    /// Leaves a ground residue entity carrying active diseases for potential contact spread.
    /// </summary>
    private void LeaveResidue(DiseaseSymptomPrototype symptom, Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease)
    {
        var cfg = symptom.LeaveResidue;
        if (!cfg.Enabled)
            return;

        var coords = Transform(ent).Coordinates;
        var residue = EntityManager.SpawnEntity("DiseaseResidueTile", coords);
        var comp = EnsureComp<DiseaseResidueComponent>(residue);

        comp.Diseases.Clear();
        var intensity = Math.Clamp(cfg.ResidueIntensity, 0.1f, 1f);
        foreach (var (id, _) in ent.Comp.ActiveDiseases)
            comp.Diseases[id] = intensity;
    }

    /// <summary>
    /// Applies symptom-configured cloud spawning if configured.
    /// </summary>
    private void ApplyCloud(DiseaseSymptomPrototype symptom, Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease)
    {
        var cfg = symptom.Cloud;
        if (!cfg.Enabled)
            return;

        // Only spawn cloud if disease can spread via air.
        if (!disease.SpreadFlags.Contains(DiseaseSpreadFlags.Airborne))
            return;

        SpawnCloud(ent, disease, cfg.Range, cfg.LifetimeSeconds, cfg.TickIntervalSeconds, disease.AirborneInfect);
    }

    /// <summary>
    /// Spawns a transient disease cloud with specified parameters at the carrier's position.
    /// </summary>
    private void SpawnCloud(Entity<DiseaseCarrierComponent> src, DiseasePrototype disease, float range, float lifetime, float tick, float chance)
    {
        var uid = EntityManager.SpawnEntity("DiseaseCloudEffect", Transform(src).Coordinates);
        var cloud = EnsureComp<DiseaseCloudComponent>(uid);
        cloud.Diseases.Clear();
        cloud.Diseases.Add(disease.ID);
        cloud.Range = range;
        cloud.TickInterval = TimeSpan.FromSeconds(tick);
        cloud.Lifetime = TimeSpan.FromSeconds(lifetime);
        cloud.NextTick = _timing.CurTime + cloud.TickInterval;
        cloud.Expiry = _timing.CurTime + cloud.Lifetime;
    }
}
