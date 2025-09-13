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
	/// Per-disease intensity map for this residue. Each disease ID maps to its current intensity [0..1].
	/// </summary>
	[DataField, ViewVariables(VVAccess.ReadWrite)]
	public Dictionary<string, float> Diseases = new();

	/// <summary>
	/// Intensity decay per second.
	/// </summary>
	[DataField]
	public float DecayPerSecond = 0.05f;

	/// <summary>
	/// Amount to reduce per-disease intensity after a contact interaction.
	/// </summary>
	[DataField]
	public float ContactReduction = 0.1f;

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
