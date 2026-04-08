using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Client.E3D.FirstPerson;

public sealed class FirstPersonInputGateSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private FirstPersonUIController Controller => _ui.GetUIController<FirstPersonUIController>();

    public bool BlocksMouseRotator(EntityUid uid)
    {
        return Controller.Enabled && _player.LocalEntity == uid;
    }
}
