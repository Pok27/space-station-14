using Content.Client.Magazine.UI;
using Content.Client.Items;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Client.Magazine.EntitySystems;

/// <summary>
/// Wires up item status logic for magazines with BallisticAmmoProviderComponent.
/// </summary>
/// <seealso cref="MagazineStatusControl"/>
public sealed class MagazineItemStatusSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<BallisticAmmoProviderComponent>(
            entity => new MagazineStatusControl(entity, EntityManager));
    }
}