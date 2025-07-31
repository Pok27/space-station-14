using System.Security.Policy;

namespace Content.Server.Botany.Components;

[RegisterComponent]
public abstract partial class PlantGrowthComponent : Component {
    /// <summary>
    /// Creates a deep copy of this component using MemberwiseClone.
    /// MemberwiseClone performs a shallow copy of all fields, which is sufficient
    /// for value types and reference types that don't need deep copying.
    /// </summary>
    /// <returns>A new instance of the same component type with copied values</returns>
    public PlantGrowthComponent DupeComponent()
    {
        return (PlantGrowthComponent)this.MemberwiseClone();
    }
}


