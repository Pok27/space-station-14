using System;
using Content.Server.Chat.Systems;
using Content.Server.Medical;
using Content.Server.Popups;
using Content.Server.Temperature.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Jittering;
using Content.Shared.Medical.Disease;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;
using Robust.Shared.Map;
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
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
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
        ApplyAirborne(symptom, ent, disease);
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
    /// <summary>
    /// Spawns a transient disease cloud if configured on the symptom.
    /// </summary>
    private void ApplyCloud(DiseaseSymptomPrototype symptom, Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease)
    {
        var cfg = symptom.Cloud;
        if (!cfg.Enabled)
            return;

        SpawnCloud(ent, disease, cfg.Range, cfg.LifetimeSeconds, cfg.TickIntervalSeconds, disease.AirborneInfect);
    }

    /// <summary>
    /// Applies symptom-configured airborne spread if configured and disease supports airborne spread.
    /// </summary>
    /// <summary>
    /// Attempts airborne spread if enabled by the symptom and disease supports airborne vector.
    /// </summary>
    private void ApplyAirborne(DiseaseSymptomPrototype symptom, Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease)
    {
        var cfg = symptom.Airborne;
        if (!cfg.Enabled)
            return;

        SpreadAirborne(ent, disease, cfg.Range, disease.AirborneInfect);
    }

    /// <summary>
    /// Spawns a transient disease cloud with specified parameters at the carrier's position.
    /// </summary>
    /// <summary>
    /// Spawns a disease cloud entity and initializes its timers and range.
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

    /// <summary>
    /// Attempts airborne spread from the source to nearby targets, honoring obstructions and PPE.
    /// </summary>
    /// <summary>
    /// Tries to infect nearby entities through air, honoring obstructions and PPE.
    /// </summary>
    private void SpreadAirborne(Entity<DiseaseCarrierComponent> source, DiseasePrototype disease, float range, float baseChance)
    {
        if (!_disease.HasSpreadFlag(disease, DiseaseSpreadFlags.Airborne))
            return;

        var mapPos = _transformSystem.GetMapCoordinates(source);
        if (mapPos.MapId == MapId.Nullspace)
            return;

        var ents = _lookup.GetEntitiesInRange(mapPos, range, LookupFlags.Dynamic | LookupFlags.Sundries);
        foreach (var other in ents)
        {
            if (other == source.Owner)
                continue;

            // Only living & unobstructed targets.
            if (!TryComp<MobStateComponent>(other, out var mobState) || mobState.CurrentState == Content.Shared.Mobs.MobState.Dead)
                continue;

            if (!_interaction.InRangeUnobstructed(source.Owner, other, range))
                continue;

            var chance = AdjustChanceForPPE(other, baseChance, disease);
            _disease.TryInfectWithChance(other, disease.ID, chance);
        }
    }

    /// <summary>
    /// Applies simple PPE modifiers (mask/head slots) to airborne infection chance.
    /// </summary>
    /// <summary>
    /// Applies simple PPE modifiers (mask/head slots) to airborne infection chance.
    /// </summary>
    private float AdjustChanceForPPE(EntityUid target, float baseChance, DiseasePrototype? disease = null)
    {
        var chance = baseChance;
        // Respect disease-level override for ignoring mask PPE
        var considerMask = disease == null || !disease.IgnoreMaskPPE;

        if (considerMask && _inventory.TryGetSlotEntity(target, "mask", out var maskUid) && TryComp<MaskComponent>(maskUid, out var mask))
        {
            if (!mask.IsToggled)
                chance *= 0.5f;
        }

        if (_inventory.TryGetSlotEntity(target, "head", out var _))
            chance *= 0.7f;

        return MathF.Max(0f, MathF.Min(1f, chance));
    }
}
