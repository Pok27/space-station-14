using Robust.Shared.GameObjects;
using Content.Shared.Medical.Disease;

namespace Content.Server.Medical.Disease;

public sealed partial class DiseaseSymptomSystem
{
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

    public void OnDiseaseCured(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease)
    {
        if (!ent.Comp.AddedComponents.TryGetValue(disease.ID, out var comps))
            return;

        foreach (var regName in comps)
        {
            if (EntityManager.ComponentFactory.TryGetRegistration(regName, out var reg))
            {
                if (EntityManager.HasComponent(ent.Owner, reg.Type))
                    RemComp(ent.Owner, reg.Type);
            }
        }

        comps.Clear();
        ent.Comp.AddedComponents.Remove(disease.ID);
    }

    public void OnSymptomCured(Entity<DiseaseCarrierComponent> ent, DiseasePrototype disease, string symptomId)
    {
        // Intentionally left blank; specific symptom variants can add behavior here later if needed.
    }
}
