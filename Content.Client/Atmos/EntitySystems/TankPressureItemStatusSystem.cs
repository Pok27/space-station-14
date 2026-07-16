using Content.Client.Atmos.UI;
using Content.Client.Items;
using Content.Shared.Atmos.Components;

namespace Content.Client.Atmos.EntitySystems;

/// <summary>
/// Wires up item status logic for <see cref="GasTankComponent"/>.
/// </summary>
/// <seealso cref="TankPressureStatusControl"/>
public sealed partial class TankPressureItemStatusSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<GasTankComponent>(entity =>
            new TankPressureStatusControl(entity, EntityManager));
    }
}
