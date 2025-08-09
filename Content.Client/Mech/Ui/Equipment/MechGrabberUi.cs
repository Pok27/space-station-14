using Content.Client.UserInterface.Fragments;
using Content.Shared.Mech;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.Mech.Ui.Equipment;

/// <summary>
/// UI fragment for mech grabber equipment.
/// </summary>
/// <seealso cref="MechGrabberUiFragment"/>
public sealed partial class MechGrabberUi : UIFragment
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private MechGrabberUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        if (fragmentOwner == null)
            return;

        IoCManager.InjectDependencies(this);

        _fragment = new MechGrabberUiFragment();

        _fragment.OnEjectAction += entityUid =>
        {
            var equipmentNetEntity = _entityManager.GetNetEntity(fragmentOwner.Value);
            var itemNetEntity = _entityManager.GetNetEntity(entityUid);
            userInterface.SendMessage(new MechGrabberEjectMessage(equipmentNetEntity, itemNetEntity));
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not MechGrabberUiState grabberState)
            return;

        _fragment?.UpdateContents(grabberState);
    }
}
