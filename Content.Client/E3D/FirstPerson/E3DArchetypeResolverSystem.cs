using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Administration.Components;
using Content.Shared.Delivery;
using Content.Shared.Doors.Components;
using Content.Shared.E3D;
using Content.Shared.E3D.Components;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.SubFloor;
using Content.Shared.Tag;
using Content.Shared.Wall;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.E3D.FirstPerson;

public sealed class E3DArchetypeResolverSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string WallTag = "Wall";
    private const string WindowTag = "Window";

    public bool TryResolve(EntityUid uid, out ResolvedE3DRenderable resolved)
    {
        resolved = default;

        if (ShouldSkipEntity(uid))
            return false;

        TryComp(uid, out SpriteComponent? sprite);
        var explicitRenderable = CompOrNull<E3DRenderableComponent>(uid);
        if (explicitRenderable is { Enabled: false })
            return false;

        var source = explicitRenderable != null
            ? E3DClassificationSource.Metadata
            : E3DClassificationSource.Inference;
        var archetype = explicitRenderable?.Archetype ?? ResolveFallbackArchetype(uid, sprite);
        var occludes = explicitRenderable?.BlocksVision ?? ResolveVisionBlocker(uid, archetype);
        var blocksInteraction = explicitRenderable?.BlocksInteraction ?? ResolveInteractionBlocker(uid, archetype);
        var transparent = explicitRenderable?.Transparent ?? archetype is E3DArchetype.Window or E3DArchetype.GasOverlay;
        if (!transparent && TryComp(uid, out PhysicsComponent? physics))
        {
            var layer = physics.CollisionLayer;
            var isGlassLayer = (layer & (int) CollisionGroup.GlassLayer) != 0 ||
                               (layer & (int) CollisionGroup.GlassAirlockLayer) != 0;
            var isOpaque = (layer & (int) CollisionGroup.Opaque) != 0;
            if (isGlassLayer && !isOpaque)
            {
                transparent = true;
            }
        }
        var anchored = Transform(uid).Anchored;

        var defaultHeight = archetype switch
        {
            E3DArchetype.Wall or E3DArchetype.SmoothWall or E3DArchetype.Edge or E3DArchetype.OccluderOnly => 1.8f,
            E3DArchetype.Door => 1.8f,
            E3DArchetype.Window or E3DArchetype.Frame => 1.6f,
            E3DArchetype.Mob => 1.45f,
            E3DArchetype.DecalLike => 0.04f,
            E3DArchetype.Table => 0.9f,
            E3DArchetype.Wallmount => 0.9f,
            E3DArchetype.Item => 0.45f,
            _ => 1.1f
        };

        var defaultWidth = archetype switch
        {
            E3DArchetype.DecalLike => 0.85f,
            E3DArchetype.Item => 0.45f,
            E3DArchetype.Wallmount => 0.65f,
            _ => 1f
        };

        var floorAnchored = explicitRenderable?.FloorAnchored ?? archetype is E3DArchetype.DecalLike or E3DArchetype.Floor or E3DArchetype.Item or E3DArchetype.Billboard or E3DArchetype.Table or E3DArchetype.Mob;
        var wallMounted = explicitRenderable?.WallMounted ?? archetype == E3DArchetype.Wallmount || HasComp<WallMountComponent>(uid);
        var preferFixtureBounds = explicitRenderable?.PreferFixtureBounds ?? anchored && archetype is E3DArchetype.Billboard or E3DArchetype.Table;
        var spriteMode = explicitRenderable?.SpriteMode ?? ResolveSpriteMode(archetype);
        var wallThickness = explicitRenderable?.WallThicknessMode ?? (archetype is E3DArchetype.Window or E3DArchetype.Frame
            ? E3DWallThicknessMode.Thin
            : E3DWallThicknessMode.Auto);

        resolved = new ResolvedE3DRenderable(
            archetype,
            explicitRenderable?.Height ?? defaultHeight,
            explicitRenderable?.Width ?? defaultWidth,
            explicitRenderable?.DepthBias ?? 0f,
            transparent,
            blocksInteraction,
            occludes,
            spriteMode,
            explicitRenderable?.EyeOffset ?? 0f,
            explicitRenderable?.WorldOffset ?? Vector2.Zero,
            wallThickness,
            floorAnchored,
            wallMounted,
            preferFixtureBounds,
            source);

        return true;
    }

    public Texture? TryGetTexture(EntityUid uid, Direction face, ResolvedE3DRenderable? resolved = null)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return null;

        var info = resolved ?? (TryResolve(uid, out var tmp) ? tmp : null);
        if (info == null)
            return null;

        if (TryGetPrimaryLayer(uid, sprite, face, out var texture, out _, out _))
            return texture;

        var icon = sprite.Icon;
        if (icon == null)
            return null;

        if (info.Value.SpriteMode is E3DSpriteMode.Directional && icon is IRsiStateLike directional)
        {
            var dir = Transform(uid).LocalRotation.GetDir();
            return directional.TextureFor(dir);
        }

        return icon.Default;
    }

    public bool TryGetBillboardVisual(EntityUid uid, Direction face, out Texture? texture, out Box2 bounds, out Color tint)
    {
        texture = null;
        bounds = default;
        tint = Color.White;

        if (!TryComp(uid, out SpriteComponent? sprite) || !IsSpriteRenderable(sprite))
            return false;

        bounds = _sprites.GetLocalBounds((uid, sprite));
        if (bounds.Size.LengthSquared() <= 0.0001f)
            bounds = Box2.CenteredAround(Vector2.Zero, Vector2.One);

        return TryGetPrimaryLayer(uid, sprite, face, out texture, out _, out tint);
    }

    public void GetVisibleLayers(EntityUid uid, Direction face, List<FpvVisualLayer> output)
    {
        GetVisibleLayers(uid, face, output, true);
    }

    public void GetVisibleLayers(EntityUid uid, Direction face, List<FpvVisualLayer> output, bool allowFallback)
    {
        output.Clear();

        if (!TryComp(uid, out SpriteComponent? sprite) || !IsSpriteRenderable(sprite))
            return;

        var worldRotation = _transform.GetWorldRotation(uid);
        var combinedBounds = _sprites.GetLocalBounds((uid, sprite));

        foreach (var entry in sprite.AllLayers)
        {
            if (entry is not SpriteComponent.Layer layer)
                continue;

            if (!_sprites.IsVisible(layer) || !layer.Visible || layer.Blank)
                continue;

            var texture = ResolveLayerTexture(layer, face, worldRotation);
            if (texture == null)
                continue;

            var bounds = _sprites.GetLocalBounds(layer);
            if (bounds.Size.LengthSquared() <= 0.0001f)
                bounds = combinedBounds;

            output.Add(new FpvVisualLayer(texture, bounds, MultiplyColor(sprite.Color, layer.Color)));
        }

        if (allowFallback && output.Count == 0 && sprite.Icon?.Default is { } fallback)
            output.Add(new FpvVisualLayer(fallback, combinedBounds, sprite.Color));
    }

    public bool IsClosedDoor(EntityUid uid)
    {
        return TryComp(uid, out DoorComponent? door) &&
               door.State is DoorState.Closed or DoorState.Closing or DoorState.Welded or DoorState.Denying;
    }

    public bool IsSpriteRenderable(SpriteComponent sprite)
    {
        return sprite.AddToTree && sprite.Visible && !sprite.ContainerOccluded;
    }

    private E3DArchetype ResolveFallbackArchetype(EntityUid uid, SpriteComponent? sprite)
    {
        if (TryComp(uid, out DoorComponent? _))
        {
            if (_tags.HasTag(uid, WindowTag))
                return E3DArchetype.Window;

            if (HasComp<WallMountComponent>(uid))
                return E3DArchetype.Frame;

            return E3DArchetype.Door;
        }

        if (TryComp(uid, out MobStateComponent? _))
            return E3DArchetype.Mob;

        if (_tags.HasTag(uid, WallTag))
            return E3DArchetype.Wall;

        if (_tags.HasTag(uid, WindowTag))
            return E3DArchetype.Window;

        if (HasComp<WallMountComponent>(uid))
            return E3DArchetype.Wallmount;

        if (TryComp(uid, out SubFloorHideComponent? subFloor) && !subFloor.IsUnderCover)
            return E3DArchetype.DecalLike;

        var depth = sprite?.DrawDepth ?? Robust.Shared.GameObjects.DrawDepth.Default;
        if (depth is (int) DrawDepth.LowFloors or
            (int) DrawDepth.BelowFloor or
            (int) DrawDepth.FloorTiles or
            (int) DrawDepth.Puddles or
            (int) DrawDepth.ThinWire or
            (int) DrawDepth.ThickWire or
            (int) DrawDepth.ThinPipe or
            (int) DrawDepth.ThinPipeAlt1 or
            (int) DrawDepth.ThinPipeAlt2 or
            (int) DrawDepth.ThickPipe)
        {
            return E3DArchetype.DecalLike;
        }

        if (depth == (int) DrawDepth.Walls)
            return E3DArchetype.Wall;

        if (depth == (int) DrawDepth.WallTops)
        {
            if (TryComp(uid, out OccluderComponent? _) || IsWallPhysics(uid))
                return E3DArchetype.Frame;

            return E3DArchetype.Wallmount;
        }

        if (depth is (int) DrawDepth.Doors or (int) DrawDepth.BlastDoors or (int) DrawDepth.Overdoors)
            return E3DArchetype.Door;

        if (depth is (int) DrawDepth.Items)
            return E3DArchetype.Item;

        if (depth is (int) DrawDepth.Mobs or (int) DrawDepth.SmallMobs or (int) DrawDepth.DeadMobs)
            return E3DArchetype.Mob;

        if (depth is (int) DrawDepth.WallMountedItems)
            return E3DArchetype.Wallmount;

        return Transform(uid).Anchored ? E3DArchetype.Billboard : E3DArchetype.Item;
    }

    private bool ShouldSkipEntity(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return true;

        if (HasComp<MarkerOneComponent>(uid) ||
            HasComp<MarkerTwoComponent>(uid) ||
            HasComp<MarkerThreeComponent>(uid) ||
            HasComp<DeliverySpawnerComponent>(uid) ||
            HasComp<GhostRoleMobSpawnerComponent>(uid))
        {
            return true;
        }

        if (TryComp(uid, out SubFloorHideComponent? subFloor) && subFloor.IsUnderCover)
            return true;

        if (TryComp(uid, out SpriteComponent? sprite) && !IsSpriteRenderable(sprite))
            return true;

        return false;
    }

    private bool TryGetPrimaryLayer(EntityUid uid, SpriteComponent sprite, Direction face, out Texture? texture, out Box2 bounds, out Color tint)
    {
        texture = null;
        bounds = default;
        tint = sprite.Color;
        var bestArea = 0f;
        var worldRotation = _transform.GetWorldRotation(uid);

        foreach (var entry in sprite.AllLayers)
        {
            if (entry is not SpriteComponent.Layer layer)
                continue;

            if (!_sprites.IsVisible(layer) || !layer.Visible || layer.Blank)
                continue;

            var candidate = ResolveLayerTexture(layer, face, worldRotation);
            if (candidate == null)
                continue;

            var layerBounds = _sprites.GetLocalBounds(layer);
            var area = MathF.Max(0.0001f, layerBounds.Width * layerBounds.Height);
            if (area < bestArea)
                continue;

            bestArea = area;
            texture = candidate;
            bounds = layerBounds;
            tint = MultiplyColor(sprite.Color, layer.Color);
        }

        return texture != null;
    }

    private static Color MultiplyColor(Color spriteColor, Color layerColor)
    {
        return new Color(spriteColor.RGBA * layerColor.RGBA);
    }

    private static Texture? ResolveLayerTexture(SpriteComponent.Layer layer, Direction face, Angle worldRotation)
    {
        if (layer.ActualState != null)
        {
            var direction = face == Direction.Invalid
                ? layer.EffectiveDirection(worldRotation)
                : layer.EffectiveDirection(layer.ActualState, worldRotation, face);

            return layer.ActualState.GetFrame(direction, layer.AnimationFrame);
        }

        if (layer.Texture != null)
            return layer.Texture;

        return null;
    }

    private bool ResolveVisionBlocker(EntityUid uid, E3DArchetype archetype)
    {
        if (TryComp(uid, out DoorComponent? door))
            return door.Occludes && IsClosedDoor(uid);

        return archetype switch
        {
            E3DArchetype.Wall or E3DArchetype.SmoothWall or E3DArchetype.Edge or E3DArchetype.OccluderOnly => true,
            E3DArchetype.Door => IsClosedDoor(uid),
            E3DArchetype.Window or E3DArchetype.Frame => false,
            _ => false
        };
    }

    private bool ResolveInteractionBlocker(EntityUid uid, E3DArchetype archetype)
    {
        if (TryComp(uid, out DoorComponent? _))
            return IsClosedDoor(uid);

        return archetype switch
        {
            E3DArchetype.Wall or E3DArchetype.SmoothWall or E3DArchetype.Edge or E3DArchetype.OccluderOnly => true,
            _ => false
        };
    }

    private static E3DSpriteMode ResolveSpriteMode(E3DArchetype archetype)
    {
        return archetype switch
        {
            E3DArchetype.Door or E3DArchetype.Window => E3DSpriteMode.Directional,
            E3DArchetype.Wall or E3DArchetype.SmoothWall or E3DArchetype.Edge or E3DArchetype.Frame => E3DSpriteMode.WallFace,
            E3DArchetype.Billboard or E3DArchetype.Item or E3DArchetype.Mob or E3DArchetype.Table or E3DArchetype.Wallmount or E3DArchetype.DecalLike => E3DSpriteMode.Billboard,
            _ => E3DSpriteMode.Auto
        };
    }

    private bool IsWallPhysics(EntityUid uid)
    {
        if (!TryComp(uid, out PhysicsComponent? physics))
            return false;

        var layer = physics.CollisionLayer;
        return (layer & (int) CollisionGroup.WallLayer) != 0 ||
               (layer & (int) CollisionGroup.SpecialWallLayer) != 0 ||
               (layer & (int) CollisionGroup.FullTileLayer) != 0;
    }
}

public readonly record struct ResolvedE3DRenderable(
    E3DArchetype Archetype,
    float Height,
    float Width,
    float DepthBias,
    bool Transparent,
    bool BlocksInteraction,
    bool BlocksVision,
    E3DSpriteMode SpriteMode,
    float EyeOffset,
    Vector2 WorldOffset,
    E3DWallThicknessMode WallThicknessMode,
    bool FloorAnchored,
    bool WallMounted,
    bool PreferFixtureBounds,
    E3DClassificationSource ClassificationSource);
