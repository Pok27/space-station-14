using Robust.Shared.GameObjects;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

[DataDefinition]
public sealed partial class SymptomAddComponent : SymptomBehavior
{
    /// <summary>
    /// Component registration name to add to the carrier.
    /// </summary>
    [DataField(required: true)]
    public string Component { get; private set; } = string.Empty;
}

public sealed partial class DiseaseSymptomSystem
{
    /// <summary>
    /// Adds a permanent component to the carrier.
    /// </summary>
    private void DoAddComponent(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease, SymptomAddComponent add)
    {
        if (string.IsNullOrWhiteSpace(add.Component))
            return;

        if (!EntityManager.ComponentFactory.TryGetRegistration(add.Component, out var reg))
            return;

        if (EntityManager.HasComponent(ent.Owner, reg.Type))
            return;

        var comp = (Component) EntityManager.ComponentFactory.GetComponent(add.Component);
        AddComp(ent.Owner, comp);

        if (!ent.Comp.AddedComponents.TryGetValue(disease.ID, out var set))
        {
            set = new HashSet<string>();
            ent.Comp.AddedComponents[disease.ID] = set;
        }
        set.Add(add.Component);
    }
}
