using System;
using Content.Server.Chat.Systems;
using Content.Server.Medical;
using Content.Server.Popups;
using Content.Server.Temperature.Systems;
using Content.Shared.Damage;
using Content.Shared.Jittering;
using Content.Shared.Medical.Disease;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.Disease;

/// <summary>
/// Encapsulates symptom-side effects and secondary spread mechanics for diseases.
/// </summary>
public sealed partial class DiseaseSymptomSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DiseaseSystem _disease = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    /// <inheritdoc/>
    /// <summary>
    /// Executes the side-effects for a triggered symptom on a carrier.
    /// </summary>
    public void TriggerSymptom(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease, DiseaseSymptomPrototype symptom)
    {
        foreach (var variant in symptom.Behaviors)
        {
            switch (variant)
            {
                case SymptomEmote emote:
                    DoEmote(ent, emote);
                    break;

                case SymptomVomit vomit:
                    DoVomit(ent, vomit);
                    break;

                case SymptomTemperature temp:
                    DoTemperature(ent, temp);
                    break;

                case SymptomNarcolepsy narco:
                    DoNarcolepsy(ent, narco);
                    break;

                case SymptomJitter jitter:
                    DoJitter(ent, jitter);
                    break;

                case SymptomDamage dmg:
                    DoDamage(ent, dmg);
                    break;

                case SymptomShout shout:
                    DoShout(ent, shout);
                    break;

                case SymptomSensation sense:
                    DoSensation(ent, sense);
                    break;

                case SymptomTransitionDisease trans:
                    DoTransitionDisease(ent, disease, trans);
                    break;

                case SymptomAddComponent addc:
                    DoAddComponent(ent, disease, addc);
                    break;

                default:
                    break;
            }
        }
        // Apply configurable effects for any symptom. If not configured in YAML, these are no-ops.
        ApplyCloud(symptom, ent, disease);
        LeaveResidue(symptom, ent, disease);
    }

    /// <summary>
    /// Leaves residue on the ground containing current carrier diseases.
    /// </summary>
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
