using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.E3D.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class E3DRenderableComponent : Component
{
    [DataField, AutoNetworkedField]
    public E3DArchetype Archetype = E3DArchetype.Billboard;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public float? Height;

    [DataField, AutoNetworkedField]
    public float? Width;

    [DataField, AutoNetworkedField]
    public float? DepthBias;

    [DataField, AutoNetworkedField]
    public bool? Transparent;

    [DataField, AutoNetworkedField]
    public bool? BlocksInteraction;

    [DataField, AutoNetworkedField]
    public bool? BlocksVision;

    [DataField, AutoNetworkedField]
    public E3DSpriteMode? SpriteMode;

    [DataField, AutoNetworkedField]
    public float? EyeOffset;

    [DataField, AutoNetworkedField]
    public Vector2? WorldOffset;

    [DataField, AutoNetworkedField]
    public E3DWallThicknessMode? WallThicknessMode;

    [DataField, AutoNetworkedField]
    public bool? FloorAnchored;

    [DataField, AutoNetworkedField]
    public bool? WallMounted;

    [DataField, AutoNetworkedField]
    public bool? PreferFixtureBounds;
}
