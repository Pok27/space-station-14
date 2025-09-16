using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Server.Medical.Disease;
using Content.Shared.Medical.Disease;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using System.Globalization;

namespace Content.Server.Administration.Commands;

/// <summary>
/// Infects your attached entity with a disease at an optional stage.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class InfectCommand : LocalizedEntityCommands
{
	public override string Command => "infect";
	public override string Description => Loc.GetString("cmd-infect-desc");
	public override string Help => Loc.GetString("cmd-infect-help");

	[Dependency] private readonly IEntitySystemManager _sysMan = default!;

	public override void Execute(IConsoleShell shell, string argStr, string[] args)
	{
		if (args.Length == 1)
		{
            shell.WriteError(Loc.GetString("cmd-infect-need-id"));
			shell.WriteLine(Help);
			return;
		}

		if (args.Length < 2)
		{
			shell.WriteError(Loc.GetString("cmd-infect-need-target"));
            shell.WriteLine(Help);
			return;
		}

		if (!NetEntity.TryParse(args[0], out var parsedNet) || !EntityManager.TryGetEntity(parsedNet, out var parsedUid))
		{
			shell.WriteError(Loc.GetString("cmd-infect-bad-target", ("value", args[0])));
			return;
		}

		var disease = _sysMan.GetEntitySystem<DiseaseSystem>();
		var targetUid = parsedUid.Value;
		var diseaseId = args[1];
        var stage = 1;
		if (!disease.Infect(targetUid, diseaseId, stage))
		{
			shell.WriteError(Loc.GetString("cmd-infect-fail"));
			return;
		}

		shell.WriteLine(Loc.GetString("cmd-infect-ok", ("target", targetUid.ToString()), ("disease", diseaseId), ("stage", stage)));
	}
}

/// <summary>
/// Grants immunity to a disease to your attached entity.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class VaccinateCommand : LocalizedEntityCommands
{
	public override string Command => "vaccinate";
	public override string Description => Loc.GetString("cmd-vaccinate-desc");
	public override string Help => Loc.GetString("cmd-vaccinate-help");

	[Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

	public override void Execute(IConsoleShell shell, string argStr, string[] args)
	{
		if (args.Length == 1)
		{
			shell.WriteError(Loc.GetString("cmd-vaccinate-need-target"));
			shell.WriteLine(Help);
			return;
		}

		if (args.Length < 2)
		{
			shell.WriteError(Loc.GetString("cmd-vaccinate-need-id"));
            shell.WriteLine(Help);
			return;
		}

		if (!NetEntity.TryParse(args[0], out var net) || !EntityManager.TryGetEntity(net, out var resolved))
		{
			shell.WriteError(Loc.GetString("cmd-vaccinate-bad-target", ("value", args[0])));
			return;
		}

		var diseaseId = args[1];
		if (!_proto.HasIndex<DiseasePrototype>(diseaseId))
		{
			shell.WriteError(Loc.GetString("cmd-vaccinate-fail"));
			return;
		}

        var targetUid = resolved.Value;
		if (!_entMan.TryGetComponent(targetUid, out DiseaseCarrierComponent? comp))
			comp = _entMan.AddComponent<DiseaseCarrierComponent>(targetUid);

		// Optional immunity strength as 3rd argument (0..1).
		var immunity = 1.0f;
		if (args.Length >= 3)
		{
			if (!float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out immunity))
			{
				shell.WriteError(Loc.GetString("cmd-vaccinate-bad-immunity", ("value", args[2])));
				return;
			}
			immunity = Math.Clamp(immunity, 0f, 1f);
		}

		comp.Immunity[diseaseId] = immunity;
		comp.ActiveDiseases.Remove(diseaseId);
		shell.WriteLine(Loc.GetString("cmd-vaccinate-ok", ("target", targetUid.ToString()), ("disease", diseaseId)));
	}
}
