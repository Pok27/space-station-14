using Content.Shared.Power.Components;
using Content.Shared.Power;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Server.Power.EntitySystems;

/// <summary>
/// Server-side system that updates <see cref="BatteryItemStatusComponent"/> with current battery charge percent
/// so that it can be displayed on the client item status panel.
/// </summary>
public sealed class BatteryItemStatusSyncSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<BatteryItemStatusComponent>();
        while (enumerator.MoveNext(out var uid, out var status))
        {
            // Retrieve battery info using the existing GetBatteryInfoEvent infrastructure
            var infoEvent = new GetBatteryInfoEvent();
            RaiseLocalEvent(uid, ref infoEvent);

            if (!infoEvent.HasBattery)
                continue;

            int percent = (int)(infoEvent.ChargePercent * 100);

            bool toggleOn = false;

            if (TryComp<ItemToggleComponent>(uid, out var itemToggle))
            {
                toggleOn = itemToggle.Activated;
            }
            else
            {
                foreach (var comp in EntityManager.GetComponents(uid))
                {
                    if (comp is IActiveItemMarker)
                    {
                        toggleOn = true;
                        break;
                    }
                }
            }

            if (percent != status.ChargePercent || toggleOn != status.ToggleOn)
            {
                status.ChargePercent = percent;
                status.ToggleOn = toggleOn;
                Dirty(uid, status);
            }
        }
    }
}