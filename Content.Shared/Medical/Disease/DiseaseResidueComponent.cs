using System;
using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Disease;

/// <summary>
/// Residual surface/item contamination by diseases. Decays over time.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DiseaseResidueComponent : Component
{
	/// <summary>
	/// Disease prototype IDs present in this residue.
	/// </summary>
	[DataField, ViewVariables(VVAccess.ReadWrite)]
	public List<string> Diseases = new();

	/// <summary>
	/// Current intensity in [0..1]. Multiplies infection chance.
	/// </summary>
	[DataField, ViewVariables(VVAccess.ReadWrite)]
	public float Intensity = 1f;

	/// <summary>
	/// Intensity decay per second.
	/// </summary>
	[DataField]
	public float DecayPerSecond = 0.05f;

	/// <summary>
	/// Base infect chance per tick, scaled by <see cref="Intensity"/>.
	/// </summary>
	[DataField]
	public float InfectChanceBase = 0.06f;

	/// <summary>
	/// Affect radius (world units) for proximity infection.
	/// </summary>
	[DataField]
	public float Range = 0.6f;

	/// <summary>
	/// How often proximity infection attempts occur.
	/// </summary>
	[DataField]
	public TimeSpan TickInterval = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Next time to run a proximity infection tick.
	/// </summary>
	[DataField]
	public TimeSpan NextTick;
}
