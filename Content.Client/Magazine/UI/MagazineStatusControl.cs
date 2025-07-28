using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Magazine.UI;

/// <summary>
/// Displays magazine ammunition information for entities with BallisticAmmoProviderComponent.
/// </summary>
/// <seealso cref="MagazineItemStatusSystem"/>
public sealed class MagazineStatusControl : PollingItemStatusControl<MagazineStatusControl.Data>
{
    private readonly Entity<BallisticAmmoProviderComponent> _parent;
    private readonly IEntityManager _entityManager;
    private readonly RichTextLabel _label;

    public MagazineStatusControl(
        Entity<BallisticAmmoProviderComponent> parent,
        IEntityManager entityManager)
    {
        _parent = parent;
        _entityManager = entityManager;
        _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
        AddChild(_label);
    }

    protected override Data PollData()
    {
        var currentRounds = _parent.Comp.Count;
        var maxRounds = _parent.Comp.Capacity;

        return new Data(currentRounds, maxRounds);
    }

    protected override void Update(in Data data)
    {
        var markup = Loc.GetString("magazine-status-rounds",
            ("current", data.CurrentRounds),
            ("max", data.MaxRounds));

        _label.SetMarkup(markup);
    }

    public readonly record struct Data(int CurrentRounds, int MaxRounds);
}