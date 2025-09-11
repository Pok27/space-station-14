using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Server.Medical.Disease;
using Content.Shared.Medical.Disease;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Server.Administration.Commands;

/// <summary>
/// Infects your attached entity with a disease at an optional stage.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class InfectCommand : IConsoleCommand
{
	public string Command => "infect";
	public string Description => Loc.GetString("cmd-infect-desc");
	public string Help => Loc.GetString("cmd-infect-help");

	[Dependency] private readonly IEntitySystemManager _sysMan = default!;
	public void Execute(IConsoleShell shell, string argStr, string[] args)
	{
		if (args.Length == 0)
		{
			shell.WriteError(Loc.GetString("cmd-vaccinate-need-id"));
			return;
		}

		var player = shell.Player;
		if (player?.AttachedEntity is not { } uid)
		{
			shell.WriteError(Loc.GetString("cmd-vaccinate-no-entity"));
			return;
		}

		var diseaseId = args[0];
		var stage = 1;
		if (args.Length >= 2 && int.TryParse(args[1], out var parsed))
			stage = parsed;

		var disease = _sysMan.GetEntitySystem<DiseaseSystem>();
		if (!disease.Infect(uid, diseaseId, stage))
		{
			shell.WriteError(Loc.GetString("cmd-infect-fail"));
			return;
		}

		shell.WriteLine(Loc.GetString("cmd-infect-ok", ("disease", diseaseId), ("stage", stage)));
	}
}

/// <summary>
/// Grants immunity to a disease to your attached entity.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class VaccinateCommand : IConsoleCommand
{
	public string Command => "vaccinate";
	public string Description => Loc.GetString("cmd-vaccinate-desc");
	public string Help => Loc.GetString("cmd-vaccinate-help");

	[Dependency] private readonly IEntityManager _entMan = default!;
	public void Execute(IConsoleShell shell, string argStr, string[] args)
	{
		if (args.Length == 0)
		{
			shell.WriteError(Loc.GetString("cmd-vaccinate-need-id"));
			return;
		}

		var player = shell.Player;
		if (player?.AttachedEntity is not { } uid)
		{
			shell.WriteError(Loc.GetString("cmd-vaccinate-no-entity"));
			return;
		}

		var diseaseId = args[0];
		if (!_entMan.TryGetComponent(uid, out DiseaseCarrierComponent? comp))
			comp = _entMan.AddComponent<DiseaseCarrierComponent>(uid);

		comp.Immunity.Add(diseaseId);
		comp.ActiveDiseases.Remove(diseaseId);
		shell.WriteLine(Loc.GetString("cmd-vaccinate-ok", ("disease", diseaseId)));
	}
}
