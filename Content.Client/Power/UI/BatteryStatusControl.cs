using Content.Shared.Power.Components;
using Content.Client.Items.UI;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Radio.Components;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Power.UI;

/// <summary>
/// Displays battery charge information for <see cref="BatteryItemStatusComponent"/>.
/// </summary>
/// <seealso cref="BatteryItemStatusSystem"/>
public sealed class BatteryStatusControl : PollingItemStatusControl<BatteryStatusControl.Data>
{
    private readonly Entity<BatteryItemStatusComponent> _parent;
    private readonly IEntityManager _entityManager;
    private readonly RichTextLabel _label;

    public BatteryStatusControl(
        Entity<BatteryItemStatusComponent> parent,
        IEntityManager entityManager)
    {
        _parent = parent;
        _entityManager = entityManager;
        _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
        AddChild(_label);
    }

    protected override Data PollData()
    {
        // Use the networked value from the status component
        var chargePercent = _parent.Comp.ChargePercent;

        // Check if item has toggle state (like stun baton)
        bool? toggleState = null;
        if (_parent.Comp.ShowToggleState)
        {
            // Prefer generic item toggle if available.
            if (_entityManager.TryGetComponent(_parent.Owner, out ItemToggleComponent? toggle))
            {
                toggleState = toggle.Activated;
            }
            // Fallback: some devices (e.g., Radio Jammer) indicate activation via a dedicated component.
            else
            {
                // Generic fallback: If entity has any component whose type name starts with "Active"
                // (e.g. ActiveFlashComponent, ActiveRadioJammerComponent, etc.) assume On.
                // Otherwise, if it has a matching non-active component (e.g. RadioJammerComponent) assume Off.
                bool hasActive = false;
                bool hasInactive = false;

                foreach (var comp in _entityManager.GetComponents(_parent.Owner))
                {
                    var name = comp.GetType().Name;
                    if (name.StartsWith("Active"))
                    {
                        hasActive = true;
                        break;
                    }
                    if (name.EndsWith("JammerComponent") || name.EndsWith("FlashComponent") || name.EndsWith("StunbatonComponent"))
                    {
                        hasInactive = true;
                    }
                }

                if (hasActive)
                    toggleState = true;
                else if (hasInactive)
                    toggleState = false;
            }
        }

        return new Data(chargePercent, toggleState);
    }

    protected override void Update(in Data data)
    {
        var markup = Loc.GetString("battery-status-charge", ("percent", data.ChargePercent));

        if (data.ToggleState.HasValue)
        {
            var toggleText = data.ToggleState.Value
                ? Loc.GetString("battery-status-on")
                : Loc.GetString("battery-status-off");
            markup += "\n" + toggleText;
        }

        _label.SetMarkup(markup);
    }

    public readonly record struct Data(int ChargePercent, bool? ToggleState);
}
