using Content.Server.Speech.Components;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server.Speech.EntitySystems;

/// <summary>
/// Applies the all-caps accent to speech and relayed speech status effect events.
/// </summary>
public sealed class AllCapsAccentSystem : RelayAccentSystem<AllCapsAccentComponent>
{
    protected override string AccentuateInternal(EntityUid uid, AllCapsAccentComponent comp, string message)
    {
        return message.ToUpperInvariant();
    }
}
