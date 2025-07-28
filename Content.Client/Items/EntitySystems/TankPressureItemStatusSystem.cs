using Content.Client.Items.Components;
using Content.Client.Items.UI;

namespace Content.Client.Items.EntitySystems;

/// <summary>
/// Wires up item status logic for <see cref="TankPressureItemStatusComponent"/>.
/// </summary>
/// <seealso cref="TankPressureStatusControl"/>
public sealed class TankPressureItemStatusSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<TankPressureItemStatusComponent>(
            entity => new TankPressureStatusControl(entity, EntityManager));
    }
}