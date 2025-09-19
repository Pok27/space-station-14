using Robust.Shared.Prototypes;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Medical.Disease;
using Content.Server.Chat.Systems;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomEmote : SymptomBehavior
{
    /// <summary>
    /// Optional emote prototype to execute.
    /// </summary>
    [DataField]
    public ProtoId<EmotePrototype>? EmoteId { get; private set; }
}

public sealed partial class SymptomEmote
{
    [Dependency] private readonly ChatSystem _chat = default!;

    /// <summary>
    /// Triggers an emote on the carrier if the symptom specifies an emote prototype.
    /// </summary>
    public override void OnSymptom(EntityUid uid, DiseasePrototype disease)
    {
        if (EmoteId is not { } emoteProto)
            return;

        _chat.TryEmoteWithChat(uid, emoteProto, ignoreActionBlocker: true);
    }
}
