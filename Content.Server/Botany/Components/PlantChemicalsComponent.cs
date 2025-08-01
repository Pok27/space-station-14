namespace Content.Server.Botany.Components;

[RegisterComponent]
[DataDefinition]
public sealed partial class PlantChemicalsComponent : Component
{
    /// <summary>
    /// The chemicals that this plant produces and their quantities.
    /// </summary>
    [DataField]
    public Dictionary<string, SeedChemQuantity> Chemicals = new();
}