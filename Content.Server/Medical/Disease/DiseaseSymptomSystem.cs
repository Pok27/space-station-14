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
using Content.Server.Temperature.Systems;
using Content.Server.Temperature.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Bed.Sleep;
using Robust.Shared.Random;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared.Damage;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Robust.Shared.Prototypes;
using Content.Shared.Timing;
using Content.Server.Speech.Components;
using Content.Shared.Dataset;

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
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

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

                case SymptomGenericStatusEffect se:
                    DoGenericStatusEffect(ent, se);
                    break;

                case SymptomAddComponent addc:
                    DoAddComponent(ent, addc);
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
    /// Unified emote action (cough or sneeze): popup, sound, jitter, residue.
    /// </summary>
    private void DoEmote(Entity<DiseaseCarrierComponent> ent, SymptomEmote emote)
    {
        var sound = emote.Sound;
        var volume = emote.SoundVolume;
        var variation = emote.SoundVariation;
        _audio.PlayPvs(sound, ent.Owner, AudioParams.Default.WithVolume(volume).WithVariation(variation));

        if (!string.IsNullOrEmpty(emote.PopupText))
            _popup.PopupEntity(Loc.GetString(emote.PopupText), ent, PopupType.Small);
    }

    /// <summary>
    /// Vomit behavior with configurable parameters.
    /// </summary>
    private void DoVomit(Entity<DiseaseCarrierComponent> ent, SymptomVomit vomit)
    {
        _vomit.Vomit(ent, force: true);
    }

    /// <summary>
    /// Jitter behavior with configurable parameters.
    /// </summary>
    private void DoJitter(Entity<DiseaseCarrierComponent> ent, SymptomJitter jitter)
    {
        var jitterSeconds = jitter.JitterSeconds;
        var jitterAmplitude = jitter.JitterAmplitude;
        var jitterFrequency = jitter.JitterFrequency;
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(jitterSeconds), false, jitterAmplitude, jitterFrequency);
    }

    private void DoTemperature(Entity<DiseaseCarrierComponent> ent, SymptomTemperature temp)
    {
        // Attempt to change entity temperature gradually towards the target by applying heat.
        // Convert a step in Kelvin to Joules via TemperatureSystem.GetHeatCapacity and ChangeHeat requires heat (J).
        if (!TryComp<TemperatureComponent>(ent.Owner, out var temperature))
            return;

        var target = temp.TargetTemperature;
        var current = temperature.CurrentTemperature;
        if (Math.Abs(current - target) < 0.01f)
            return;

        // apply a bounded step each trigger
        var degrees = Math.Sign(target - current) * Math.Min(Math.Abs(target - current), temp.StepTemperature);

        // heat energy = degrees * heatCapacity
        var heatCap = _temperature.GetHeatCapacity(ent.Owner);
        var heat = degrees * heatCap;
        _temperature.ChangeHeat(ent.Owner, heat, ignoreHeatResistance: true, temperature);
    }

    private void DoNarcolepsy(Entity<DiseaseCarrierComponent> ent, SymptomNarcolepsy narco)
    {
        // Chance to force sleep using the sleeping system's TrySleeping and add forced sleeping status effect
        if (!_random.Prob(narco.SleepChance))
            return;

        // Apply forced sleeping status effect entity-side via StatusEffectsSystem
        var dur = TimeSpan.FromSeconds(narco.SleepDurationSeconds);
        _status.TryAddStatusEffectDuration(ent.Owner, SleepingSystem.StatusEffectForcedSleeping, dur);
    }

    private void DoDamage(Entity<DiseaseCarrierComponent> ent, SymptomDamage dmg)
    {
        if (dmg.Damage == null || dmg.Damage.Empty)
            return;

        _damageable.TryChangeDamage(ent.Owner, new DamageSpecifier(dmg.Damage));
    }

    private void DoShout(Entity<DiseaseCarrierComponent> ent, SymptomShout shout)
    {
        if (!_prototypeManager.Resolve(shout.Pack, out var pack))
            return;

        var message = Loc.GetString(_random.Pick(pack.Values));
        _chat.TrySendInGameICMessage(ent.Owner, message, InGameICChatType.Speak, shout.HideChat);
    }

    private void DoSensation(Entity<DiseaseCarrierComponent> ent, SymptomSensation sense)
    {
        var text = Loc.GetString(sense.Popup);
        if (string.IsNullOrEmpty(text))
            return;

        _popup.PopupEntity(text, ent, sense.PopupType);
    }

    private void DoGenericStatusEffect(Entity<DiseaseCarrierComponent> ent, SymptomGenericStatusEffect se)
    {
        if (string.IsNullOrWhiteSpace(se.Key))
            return;

        // Use legacy status effect system API
        // Add/remove/set time depending on Type
        var time = TimeSpan.FromSeconds(Math.Max(0.01f, se.TimeSeconds));
        var status = EntityManager.System<Content.Shared.StatusEffect.StatusEffectsSystem>();

        switch (se.Type)
        {
            case StatusEffectApplyType.Add:
                if (!string.IsNullOrEmpty(se.Component))
                    status.TryAddStatusEffect(ent.Owner, se.Key, time, se.Refresh, se.Component);
                else
                    status.TryAddStatusEffect(ent.Owner, se.Key, time, se.Refresh);
                break;

            case StatusEffectApplyType.Remove:
                status.TryRemoveTime(ent.Owner, se.Key, time);
                break;

            case StatusEffectApplyType.Set:
                status.TrySetTime(ent.Owner, se.Key, time);
                break;
        }
    }

    private void DoAddComponent(Entity<DiseaseCarrierComponent> ent, SymptomAddComponent add)
    {
        if (string.IsNullOrWhiteSpace(add.Component))
            return;

        if (!EntityManager.ComponentFactory.TryGetRegistration(add.Component, out var reg))
            return;

        if (!EntityManager.HasComponent(ent.Owner, reg.Type))
        {
            var comp = (Component) EntityManager.ComponentFactory.GetComponent(add.Component);
            AddComp(ent.Owner, comp);
        }
    }

    /// <summary>
    /// Replace current disease with another disease prototype.
    /// </summary>
    private void DoTransitionDisease(Entity<DiseaseCarrierComponent> ent, DiseasePrototype current, SymptomTransitionDisease trans)
    {
        if (string.IsNullOrWhiteSpace(trans.Disease) || trans.Disease == current.ID)
            return;

        // Remove current disease if present.
        if (ent.Comp.ActiveDiseases.ContainsKey(current.ID))
            ent.Comp.ActiveDiseases.Remove(current.ID);

        // Infect with the new disease starting at provided stage.
        // Use the centralized DiseaseSystem to ensure proper initialization.
        _disease.Infect(ent.Owner, trans.Disease, Math.Max(1, trans.StartStage));
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
    /// Applies symptom-configured airborne spread if configured and disease supports airborne spread.
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
