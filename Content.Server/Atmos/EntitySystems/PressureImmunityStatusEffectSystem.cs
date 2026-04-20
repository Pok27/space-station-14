using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
/// Responds to pressure immunity queries for the active status effect.
/// </summary>
public sealed class PressureImmunityStatusEffectSystem : EntitySystem
{
    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<PressureImmunityStatusEffectComponent, StatusEffectRelayedEvent<GetPressureImmunityEvent>>(OnGetPressureImmunity);
    }

    private void OnGetPressureImmunity(Entity<PressureImmunityStatusEffectComponent> ent, ref StatusEffectRelayedEvent<GetPressureImmunityEvent> args)
    {
        var ev = args.Args;
        ev.IsImmune = true;
        ev.Handled = true;
        args.Args = ev;
    }
}
