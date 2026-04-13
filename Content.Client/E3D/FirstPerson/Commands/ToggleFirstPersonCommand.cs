using Content.Shared.Administration;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client.E3D.FirstPerson.Commands;

[AnyCommand]
public sealed class ToggleFirstPersonCommand : IConsoleCommand
{
    public string Command => "fpv.toggle";
    public string Description => "Toggle first-person pseudo-3D debug view overlay.";
    public string Help => "fpv.toggle";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var ui = IoCManager.Resolve<IUserInterfaceManager>();
        var controller = ui.GetUIController<FirstPersonUIController>();
        controller.Toggle();
    }
}

