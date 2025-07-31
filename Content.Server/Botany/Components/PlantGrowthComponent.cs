using System.Security.Policy;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Server.Botany.Components;

[RegisterComponent]
[DataDefinition]
public abstract partial class PlantGrowthComponent : Component {
    /// <summary>
    /// Creates a copy of this component.
    /// </summary>
    public PlantGrowthComponent DupeComponent()
    {
        // Use MemberwiseClone for now, but this might need to be improved
        return (PlantGrowthComponent)this.MemberwiseClone();
    }
}


