using Robust.Shared.Serialization;

namespace Content.Shared.Mech.Components;

/// <summary>
/// Defines the type of energy generation provided by a mech generator module.
/// </summary>
[Serializable, NetSerializable]
public enum MechGenerationType
{
	TeslaRelay,
	PlasmaGenerator
}

/// <summary>
/// Unified configuration for mech generator modules. Controls how the module supplies.
/// </summary>
[RegisterComponent]
public sealed partial class MechGeneratorModuleComponent : Component
{
	/// <summary>
	/// Selects the generator mode.
	/// </summary>
	[DataField]
	public MechGenerationType GenerationType;

	/// <summary>
	/// Output rate in watt-seconds per second added to the mech's internal battery when active.
	/// </summary>
	[DataField]
	public float ChargeRate = 20f;

	/// <summary>
	/// For Tesla relay mode only: search radius (in tiles) to detect a powered APC.
	/// </summary>
	[DataField]
	public float Radius = 3f;
}
