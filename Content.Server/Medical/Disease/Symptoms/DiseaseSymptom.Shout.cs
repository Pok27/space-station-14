using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Content.Shared.Medical.Disease;
using Robust.Shared.Random;
using Content.Shared.Chat;
using Content.Server.Chat.Systems;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomShout : SymptomBehavior
{
    /// <summary>
    /// Dataset of localized lines to shout.
    /// </summary>
    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? Pack { get; private set; }

    /// <summary>
    /// If true, suppress chat window output (bubble only).
    /// </summary>
    [DataField]
    public bool HideChat { get; private set; } = true;
}

public sealed partial class DiseaseSymptomSystem
{
    /// <summary>
    /// Makes the carrier shout a randomly picked localized line.
    /// </summary>
    private void DoShout(Entity<DiseaseCarrierComponent> ent, SymptomShout shout)
    {
        if (!_prototypeManager.Resolve(shout.Pack, out var pack))
            return;

        var message = Loc.GetString(_random.Pick(pack.Values));
        _chat.TrySendInGameICMessage(ent.Owner, message, InGameICChatType.Speak, shout.HideChat);
    }
}


