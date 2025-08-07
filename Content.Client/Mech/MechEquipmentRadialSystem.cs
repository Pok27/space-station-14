using Content.Client.Mech.Ui;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Robust.Shared.GameObjects;

namespace Content.Client.Mech.Systems;

public sealed class MechEquipmentRadialSystem : EntitySystem
{
    private MechEquipmentRadialUIController? _uiController;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechComponent, MechOpenEquipmentRadialEvent>(OnOpenEquipmentRadial);
    }

    private void OnOpenEquipmentRadial(EntityUid uid, MechComponent component, MechOpenEquipmentRadialEvent args)
    {
        _uiController ??= new MechEquipmentRadialUIController();

        _uiController.OpenRadialMenu(uid);
    }
}
