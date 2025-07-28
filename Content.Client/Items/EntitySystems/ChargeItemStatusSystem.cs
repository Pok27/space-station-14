using Content.Client.Items.Components;
using Content.Client.Items.UI;
using Content.Shared.Charges.Systems;

namespace Content.Client.Items.EntitySystems;

/// <summary>
/// Wires up item status logic for <see cref="ChargeItemStatusComponent"/>.
/// </summary>
/// <seealso cref="ChargeStatusControl"/>
public sealed class ChargeItemStatusSystem : EntitySystem
{
    [Dependency] private readonly SharedChargesSystem _chargesSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<ChargeItemStatusComponent>(
            entity => new ChargeStatusControl(entity, EntityManager, _chargesSystem));
    }
}