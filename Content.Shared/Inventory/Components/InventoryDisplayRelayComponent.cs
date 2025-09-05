using Robust.Shared.GameStates;

namespace Content.Shared.Inventory.Components;

/// <summary>
/// When present on a controlled entity, indicates that its HUD should display inventory slots
/// from another source entity (e.g., the pilot while controlling a mech).
/// Optionally, slot interactions can be proxied to the source.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InventoryDisplayRelayComponent : Component
{
    public override bool SendOnlyToOwner => true;

    /// <summary>
    /// The entity whose inventory slots should be displayed for this client.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Source;

    /// <summary>
    /// If true, inventory slot interactions (use slot, open storage, slot item interactions) will execute
    /// as if they originated from <see cref="Source"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool InteractAsSource = false;
}


