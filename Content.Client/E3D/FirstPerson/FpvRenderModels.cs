using System.Numerics;
using Content.Shared.E3D;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client.E3D.FirstPerson;

public readonly record struct FpvCameraState(
    Vector2 EyePos,
    MapId MapId,
    Angle Yaw,
    Angle Pitch,
    float EyeHeight,
    float FovDegrees,
    float MaxDistance,
    float InteractionDistance,
    int ColumnStep,
    bool FloorEnabled,
    bool BillboardEnabled,
    FirstPersonLightingMode LightingMode,
    FirstPersonQualityPreset QualityPreset,
    int LogicalColumns,
    int MaxBillboards,
    bool EnableFloorPass);

public readonly record struct FpvRayHit(
    EntityUid HitEntity,
    Vector2 HitPos,
    float Distance,
    bool VerticalSide,
    Direction WallFace);

public readonly record struct FpvSurfaceSpan(
    int ScreenX,
    int ScreenWidth,
    float Distance,
    float CorrectedDistance,
    float Top,
    float Height,
    Color Color,
    Texture? Texture,
    UIBox2? TextureRegion,
    bool Transparent,
    EntityUid Entity,
    Direction WallFace);

public readonly record struct FpvBillboard(
    EntityUid Entity,
    UIBox2 ScreenRect,
    float Distance,
    float SortDepth,
    int DrawDepth,
    bool Transparent,
    Texture? Texture,
    Color Color,
    Box2 CombinedBounds,
    Direction Face);

public readonly record struct FpvVisualLayer(
    Texture Texture,
    Box2 Bounds,
    Color Color);

public readonly record struct FpvFloorDecal(
    EntityUid Entity,
    Texture Texture,
    Vector2 WorldPosition,
    float WorldWidth,
    float WorldDepth,
    Angle Rotation,
    float Distance,
    Color Color);

public readonly record struct FpvInteractionHit(
    EntityUid? Target,
    MapCoordinates Coordinates,
    float Distance);

public readonly record struct FpvFloorSample(
    Texture Texture,
    UIBox2 TextureRegion,
    float FracX,
    float FracY);
