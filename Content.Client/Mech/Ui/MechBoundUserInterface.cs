using Content.Client.UserInterface;
using Content.Client.UserInterface.Fragments;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using JetBrains.Annotations;
using Robust.Client.Timing;
using Robust.Client.UserInterface;

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
        _menu.OnRemoveModuleButtonPressed += uid =>
        {
            _pred!.SendMessage(new MechModuleRemoveMessage(EntMan.GetNetEntity(uid)));
        };

        _menu.OnAirtightChanged += isAirtight =>
        {
            _pred!.SendMessage(new MechAirtightMessage(isAirtight));
        };

        _menu.OnFanToggle += isActive =>
        {
            _pred!.SendMessage(new MechFanToggleMessage(isActive));
        };

        _menu.OnCabinPurge += () =>
        {
            _pred!.SendMessage(new MechCabinPurgeMessage());
        };

        _menu.OnDnaLockRegister += () =>
        {
            _pred!.SendMessage(new MechDnaLockRegisterMessage());
        };

        _menu.OnDnaLockToggle += () =>
        {
            _pred!.SendMessage(new MechDnaLockToggleMessage());
        };

        _menu.OnDnaLockReset += () =>
        {
            _pred!.SendMessage(new MechDnaLockResetMessage());
        };

        _menu.OnCardLockRegister += () =>
        {
            _pred!.SendMessage(new MechCardLockRegisterMessage());
        };

        _menu.OnCardLockToggle += () =>
        {
            _pred!.SendMessage(new MechCardLockToggleMessage());
        };

        _menu.OnCardLockReset += () =>
        {
            _pred!.SendMessage(new MechCardLockResetMessage());
        };

        _menu.OnFilterToggle += enabled =>
        {
            _pred!.SendMessage(new MechFilterToggleMessage(enabled));
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
            if (predMsg is MechModuleRemoveMessage removeModMsg)
                msg.Modules.Remove(removeModMsg.Module);
        }

        _menu.UpdateState(msg);
        _menu.UpdateMechStats();

        _menu.UpdateEquipmentView(msg.Equipment);
        _menu.UpdateModuleView(msg.Modules);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (_menu == null)
            return;
        if (message is MechAccessSyncMessage access)
        {
            _menu.OverrideAccessAndRefresh(access.HasAccess);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _menu?.Close();
    }
}
