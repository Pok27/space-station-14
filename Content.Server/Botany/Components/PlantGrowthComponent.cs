using System.Security.Policy;
using Robust.Shared.Serialization;

namespace Content.Server.Botany.Components;

[RegisterComponent]
[ImplicitDataDefinitionForInheritors]
public abstract partial class PlantGrowthComponent : Component {
    /// <summary>
    /// Creates a copy of this component.
    /// </summary>
    public PlantGrowthComponent DupeComponent()
    {
        return (PlantGrowthComponent)this.MemberwiseClone();
    }
}


