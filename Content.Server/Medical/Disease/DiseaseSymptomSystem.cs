using System;
using Content.Server.Medical;
using Content.Server.Popups;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Interaction;
using Content.Shared.Jittering;
using Content.Shared.Medical.Disease;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Speech.EntitySystems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared.Humanoid;

namespace Content.Server.Medical.Disease;

/// <summary>
/// Encapsulates symptom-side effects and secondary spread mechanics for diseases.
/// </summary>
public sealed class DiseaseSymptomSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DiseaseSystem _disease = default!;

    private static readonly SoundSpecifier CoughMale = new SoundCollectionSpecifier("MaleCoughs");
    private static readonly SoundSpecifier CoughFemale = new SoundCollectionSpecifier("FemaleCoughs");
    private static readonly SoundSpecifier SneezeMale = new SoundCollectionSpecifier("MaleSneezes");
    private static readonly SoundSpecifier SneezeFemale = new SoundCollectionSpecifier("FemaleSneezes");

    /// <summary>
    /// Executes the side-effects for a triggered symptom on a carrier.
    /// </summary>
    public void TriggerSymptom(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease, DiseaseSymptomPrototype symptom)
    {
        foreach (var variant in symptom.Behaviors)
        {
            switch (variant)
            {
                case SymptomExhale exhale:
                    DoExhale(ent, exhale);
                    break;

                case SymptomVomit vomit:
                    DoVomit(ent, vomit);
                    break;

                case SymptomFever fever:
                    DoFever(ent, fever);
                    break;

                case SymptomJitter jitter:
                    DoJitter(ent, jitter);
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
    /// Unified exhale action (cough or sneeze): popup, sound, jitter, residue.
    /// </summary>
    private void DoExhale(Entity<DiseaseCarrierComponent> ent, SymptomExhale exhale)
    {
        var key = exhale.PopupText;
        _popup.PopupEntity(Loc.GetString(key), ent, PopupType.Small);

        var volume = exhale.SoundVolume;
        var variation = exhale.SoundVariation;
        PlayGenderedSound(ent, CoughMale, CoughFemale, volume, variation);

        _jitter.DoJitter(ent, TimeSpan.FromSeconds(2), refresh: false, amplitude: 6f, frequency: 3f, forceValueChange: false);
    }

    /// <summary>
    /// Vomit behavior with configurable parameters.
    /// </summary>
    private void DoVomit(Entity<DiseaseCarrierComponent> ent, SymptomVomit vomit)
    {
        _vomit.Vomit(ent, force: true);
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(3), refresh: false, amplitude: 8f, frequency: 3.5f, forceValueChange: false);
    }

    /// <summary>
    /// Fever behavior with configurable parameters.
    /// </summary>
    private void DoFever(Entity<DiseaseCarrierComponent> ent, SymptomFever fever)
    {
        _popup.PopupEntity(Loc.GetString("disease-fever"), ent, PopupType.Medium);
        _stutter.DoStutter(ent, TimeSpan.FromSeconds(8), refresh: false);
    }

    private void DoJitter(Entity<DiseaseCarrierComponent> ent, SymptomJitter jitter)
    {
        var jitterSeconds = jitter.JitterSeconds;
        var jitterAmplitude = jitter.JitterAmplitude;
        var jitterFrequency = jitter.JitterFrequency;
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(jitterSeconds), false, jitterAmplitude, jitterFrequency);
    }

    /// <summary>
    /// Plays a gendered sound collection; currently defaults to male collection.
    /// </summary>
    private void PlayGenderedSound(EntityUid uid, SoundSpecifier male, SoundSpecifier female, float volume, float variation)
    {
        _audio.PlayPvs(male, uid, AudioParams.Default.WithVolume(volume).WithVariation(variation));
    }

    /// <summary>
    /// Leaves residue on the ground containing current carrier diseases.
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
        foreach (var (id, _) in ent.Comp.ActiveDiseases)
            comp.Diseases.Add(id);

        comp.Intensity = Math.Clamp(cfg.ResidueIntensity, 0.1f, 1f);
    }

    /// <summary>
    /// Applies symptom-configured cloud spawning if configured.
    /// </summary>
    private void ApplyCloud(DiseaseSymptomPrototype symptom, Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease)
    {
        var cfg = symptom.Cloud;
        if (!cfg.Enabled)
            return;

        SpawnCloud(ent, disease, cfg.Range, cfg.LifetimeSeconds, cfg.TickIntervalSeconds, cfg.InfectChance);
    }

    /// <summary>
    /// Applies symptom-configured airborne spread if configured and disease supports airborne spread.
    /// </summary>
    private void ApplyAirborne(DiseaseSymptomPrototype symptom, Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease)
    {
        var cfg = symptom.Airborne;
        if (!cfg.Enabled)
            return;

        SpreadAirborne(ent, disease, cfg.Range, cfg.BaseChance);
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
        cloud.InfectChance = chance;
        cloud.TickInterval = TimeSpan.FromSeconds(tick);
        cloud.Lifetime = TimeSpan.FromSeconds(lifetime);
        cloud.NextTick = _timing.CurTime + cloud.TickInterval;
        cloud.Expiry = _timing.CurTime + cloud.Lifetime;
    }

    /// <summary>
    /// Attempts airborne spread from the source to nearby targets, honoring obstructions and PPE.
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
            _disease.TryInfectWithChance(other, disease.ID, chance, 1);
        }
    }

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
