using Content.Client.Items.Components;
using Content.Client.Items.UI;

namespace Content.Client.Items.EntitySystems;

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