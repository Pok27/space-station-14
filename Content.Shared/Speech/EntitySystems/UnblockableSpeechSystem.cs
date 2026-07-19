using Content.Shared.Speech.Components;
using Content.Shared.Chat;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class SpeakOnActionSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnCheck(Entity<UnblockableSpeechComponent> ent, ref CheckIgnoreSpeechBlockerEvent args)
    {
        args.IgnoreBlocker = true;
    }
}
