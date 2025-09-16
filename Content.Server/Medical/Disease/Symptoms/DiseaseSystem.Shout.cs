using Content.Shared.Medical.Disease;
using Robust.Shared.Random;
using Content.Shared.Chat;
using Content.Server.Chat.Systems;

namespace Content.Server.Medical.Disease;

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
