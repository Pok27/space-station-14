using Robust.Shared.GameObjects;

namespace Content.Server.Mech.Components;

[RegisterComponent]
public sealed partial class MechGeneratorTelemetryComponent : Component
{
	[ViewVariables(VVAccess.ReadWrite)]
	public float Current;

	[ViewVariables(VVAccess.ReadWrite)]
	public float Max;
}
