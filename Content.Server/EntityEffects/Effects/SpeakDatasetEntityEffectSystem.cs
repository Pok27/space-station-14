using Content.Server.Chat.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.EntityEffects.Effects;

/// <summary>
/// Causes the entity to speak a random localized line from the given dataset immediately.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class SpeakDatasetEntityEffectSystem : EntityEffectSystem<MetaDataComponent, SpeakDataset>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<SpeakDataset> args)
    {
        if (!_prototypeManager.TryIndex(args.Effect.PackId, out var pack))
            return;

        var message = Loc.GetString(_random.Pick(pack.Values));
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
