using Content.Client.UserInterface.Fragments;
using Content.Shared.Mech;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.Mech.Ui.Equipment;

/// <summary>
/// UI fragment for mech soundboard equipment.
/// </summary>
/// <seealso cref="MechSoundboardUiFragment"/>
public sealed partial class MechSoundboardUi : UIFragment
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private MechSoundboardUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        if (_fragment == null || _fragment.Disposed)
            _fragment = new MechSoundboardUiFragment();
        return _fragment;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        if (fragmentOwner == null)
            return;

        IoCManager.InjectDependencies(this);

        if (_fragment == null || _fragment.Disposed)
            _fragment = new MechSoundboardUiFragment();
        _fragment.OnPlayAction += soundIndex =>
        {
            var equipmentNetEntity = _entityManager.GetNetEntity(fragmentOwner.Value);
            userInterface.SendMessage(new MechSoundboardPlayMessage(equipmentNetEntity, soundIndex));
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not MechSoundboardUiState soundboardState)
            return;

        _fragment?.UpdateContents(soundboardState);
    }
}
