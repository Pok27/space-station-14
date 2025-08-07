using Content.Client.UserInterface.Fragments;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
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
    private TimeSpan _lastStatsUpdate;

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

        _menu.OnFanToggle += isActive =>
        {
            _pred!.SendMessage(new MechFanToggleMessage(isActive));
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

        // Throttle stats updates to avoid UI spam (e.g., fan energy consumption)
        var timing = IoCManager.Resolve<IClientGameTiming>();
        if (timing.CurTime - _lastStatsUpdate > TimeSpan.FromSeconds(0.25))
        {
            _menu.UpdateMechStats();
            _lastStatsUpdate = timing.CurTime;
        }

        _menu.UpdateEquipmentView(msg.Equipment);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _menu?.Dispose();
    }
}
