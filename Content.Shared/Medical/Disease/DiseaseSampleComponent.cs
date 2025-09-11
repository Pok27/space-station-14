using System.Collections.Generic;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Disease;

/// <summary>
/// Represents a collected disease sample, storing disease prototype IDs.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DiseaseSampleComponent : Component
{
	/// <summary>
	/// Determines whether there is a sample on the swab.
	/// </summary>
	[DataField, ViewVariables(VVAccess.ReadWrite)]
	public bool HasSample;

	/// <summary>
	/// Display name of the sampled subject at the time of sampling.
	/// </summary>
	[DataField, ViewVariables(VVAccess.ReadWrite)]
	public string? SubjectName;

	/// <summary>
	/// DNA string of the sampled subject at the time of sampling.
	/// </summary>
	[DataField, ViewVariables(VVAccess.ReadWrite)]
	public string? SubjectDNA;

	/// <summary>
	/// Disease prototype IDs captured by this sample (from a swab etc.).
	/// </summary>
	[DataField, ViewVariables(VVAccess.ReadWrite)]
	public List<string> Diseases = new();

	/// <summary>
	/// Optional per-disease stage captured at the moment of sampling.
	/// Key is disease prototype ID, value is stage (>= 1).
	/// </summary>
	[DataField, ViewVariables(VVAccess.ReadWrite)]
	public Dictionary<string, int> Stages = new();
}
