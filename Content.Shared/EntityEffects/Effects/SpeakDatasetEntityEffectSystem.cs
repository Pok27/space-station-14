using Content.Shared.Chat;
using Content.Shared.Dataset;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.EntityEffects.Effects;

/// <summary>
/// Causes the entity to speak a random localized line from the given dataset immediately.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class SpeakDatasetEntityEffectSystem : EntityEffectSystem<MetaDataComponent, SpeakDataset>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<SpeakDataset> args)
    {
        if (!_prototype.TryIndex(args.Effect.PackId, out var pack))
            return;
        if (pack.Values.Count == 0)
            return;
        // TODO: Replace with RandomPredicted once the engine PR is merged
        var seed = SharedRandomExtensions.HashCodeCombine([(int)entity.Owner, pack.Values.Count]);
        var rand = new System.Random(seed);
        var message = Loc.GetString(pack.Values[rand.Next(pack.Values.Count)]);
        _chat.TrySendInGameICMessage(entity.Owner, message, InGameICChatType.Speak, args.Effect.HideInChat);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class SpeakDataset : EntityEffectBase<SpeakDataset>
{
    /// <summary>
    /// Dataset of localized lines to speak.
    /// </summary>
    [DataField("pack", required: true)]
    public ProtoId<LocalizedDatasetPrototype> PackId;

    /// <summary>
    /// If true, suppress chat window output.
    /// </summary>
    [DataField]
    public bool HideInChat = false;
}
