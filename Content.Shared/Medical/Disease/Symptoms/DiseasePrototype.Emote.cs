using Robust.Shared.Prototypes;
using Content.Shared.Chat.Prototypes;

namespace Content.Shared.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomEmote : SymptomBehavior
{
    /// <summary>
    /// Optional emote prototype to execute.
    /// </summary>
    [DataField]
    public ProtoId<EmotePrototype>? EmoteId { get; private set; }
}
