using Content.Client.Weapons.Ranged.UI;
using Content.Client.Items;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Client.Weapons.Ranged.EntitySystems;

/// <summary>
/// Wires up item status logic for <see cref="BallisticAmmoProviderComponent"/>.
/// </summary>
/// <seealso cref="MagazineStatusControl"/>
public sealed partial class MagazineItemStatusSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<BallisticAmmoProviderComponent>(entity => new MagazineStatusControl(entity, EntityManager));
    }
}
