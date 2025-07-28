using Content.Client.Power.Components;
using Content.Client.Power.UI;

namespace Content.Client.Power.EntitySystems;

/// <summary>
/// Wires up item status logic for <see cref="BatteryItemStatusComponent"/>.
/// </summary>
/// <seealso cref="BatteryStatusControl"/>
public sealed class BatteryItemStatusSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<BatteryItemStatusComponent>(
            entity => new BatteryStatusControl(entity, EntityManager));
    }
}