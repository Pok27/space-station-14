using System;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

public sealed partial class DiseaseSymptomSystem
{
    /// <summary>
    /// Triggers an emote on the carrier if the symptom specifies an emote prototype.
    /// </summary>
    private void DoEmote(Entity<DiseaseCarrierComponent> ent, SymptomEmote emote)
    {
        if (emote.EmoteId is { } emoteProto)
            _chat.TryEmoteWithChat(ent.Owner, emoteProto, ignoreActionBlocker: true);
    }
}
