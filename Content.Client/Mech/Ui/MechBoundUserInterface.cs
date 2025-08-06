using Content.Client.UserInterface.Fragments;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.Timing;
using Content.Client.UserInterface;

namespace Content.Client.Mech.Ui;

[UsedImplicitly]
public sealed class MechBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MechMenu? _menu;
    private BuiPredictionState? _pred;

    public MechBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindowCenteredLeft<MechMenu>();
        _menu.SetEntity(Owner);

        _pred = new BuiPredictionState(this, IoCManager.Resolve<IClientGameTiming>());

        _menu.OnRemoveButtonPressed += uid =>
        {
            _pred!.SendMessage(new MechEquipmentRemoveMessage(EntMan.GetNetEntity(uid)));
        };

        _menu.OnAirtightChanged += isAirtight =>
        {
            _pred!.SendMessage(new MechAirtightMessage(isAirtight));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MechBoundUiState msg || _menu == null)
            return;

        foreach (var predMsg in _pred!.MessagesToReplay())
        {
            if (predMsg is MechEquipmentRemoveMessage removeMsg)
                msg.Equipment.Remove(removeMsg.Equipment);
        }

        _menu.UpdateState(msg);
        _menu.UpdateMechStats();
        _menu.UpdateEquipmentView(msg.Equipment);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _menu?.Dispose();
    }
}
