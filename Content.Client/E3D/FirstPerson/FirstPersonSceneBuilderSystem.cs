using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Resources;
using Content.Shared.Doors.Components;
using Content.Shared.E3D;
using Content.Shared.E3D.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using PhysicsTransform = Robust.Shared.Physics.Transform;

namespace Content.Client.E3D.FirstPerson;

public sealed class FirstPersonSceneBuilderSystem : EntitySystem
{
    [Dependency] private readonly E3DArchetypeResolverSystem _resolver = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _nearbyEntities = new();
    private readonly List<FpvVisualLayer> _billboardLayers = new();

    public SharedTransformSystem TransformSystem => _transform;
    public SharedMapSystem MapSystem => _map;

    public FpvCameraState BuildCameraState(FirstPersonViewControl control)
    {
        var eye = _eye.CurrentEye;
        return new FpvCameraState(
            eye.Position.Position,
            eye.Position.MapId,
            control.LookYaw,
            control.LookPitch,
            control.EyeHeight,
            control.FovDegrees,
            control.MaxDistance,
            control.InteractionDistance,
            control.ColumnStep,
            control.FloorEnabled,
            control.BillboardEnabled,
            control.LightingMode,
            control.QualityPreset,
            control.LogicalColumns,
            control.MaxBillboards,
            control.EnableFloorPass);
    }

    public bool TryCastSurfaceRay(FpvCameraState camera, float xPx, int widthPx, out FpvRayHit hit)
    {
        hit = default;
        if (camera.MapId == MapId.Nullspace)
            return false;

        var rayAngle = GetRayAngle(camera, xPx, widthPx);
        return TryCastGridRay(camera, rayAngle.ToVec(), false, out hit);
    }

    public bool TryCastOpaqueSurfaceRay(FpvCameraState camera, float xPx, int widthPx, out FpvRayHit hit)
    {
        hit = default;
        if (camera.MapId == MapId.Nullspace)
            return false;

        var rayAngle = GetRayAngle(camera, xPx, widthPx);
        return TryCastGridRay(camera, rayAngle.ToVec(), true, out hit);
    }

    public bool TryCastInteractionRay(FpvCameraState camera, out FpvInteractionHit hit)
    {
        hit = default;
        var hasWallHit = TryCastSurfaceRay(camera, 0.5f, 1, out var wallHit);
        var maxDistance = hasWallHit
            ? MathF.Min(camera.InteractionDistance, wallHit.Distance)
            : camera.InteractionDistance;

        if (TryFindInteractableEntity(camera, maxDistance, out var entity, out var coords))
        {
            hit = new FpvInteractionHit(entity, new MapCoordinates(coords, camera.MapId), (coords - camera.EyePos).Length());
            return true;
        }

        if (!hasWallHit)
            return false;

        hit = new FpvInteractionHit(wallHit.HitEntity, new MapCoordinates(wallHit.HitPos, camera.MapId), wallHit.Distance);
        return true;
    }

    public void CollectBillboards(FpvCameraState camera, float[] depthBuffer, int screenWidth, int screenHeight, List<FpvBillboard> output)
    {
        output.Clear();
        if (!camera.BillboardEnabled || camera.MapId == MapId.Nullspace)
            return;

        var projectionPlane = GetProjectionPlaneDistance(screenWidth, camera.FovDegrees);
        var right = camera.Yaw + Angle.FromDegrees(90f);
        var maxDistanceSquared = camera.MaxDistance * camera.MaxDistance;
        _nearbyEntities.Clear();
        _lookup.GetEntitiesInRange(camera.MapId, camera.EyePos, camera.MaxDistance + 0.75f, _nearbyEntities, LookupFlags.Uncontained | LookupFlags.Approximate);

        foreach (var uid in _nearbyEntities)
        {
            if (!TryComp(uid, out TransformComponent? xform) ||
                !TryComp(uid, out SpriteComponent? sprite) ||
                xform.MapID != camera.MapId ||
                uid == xform.ParentUid)
            {
                continue;
            }

            if (!_resolver.IsSpriteRenderable(sprite))
                continue;

            var world = _transform.GetMapCoordinates(uid, xform).Position;
            var rel = world - camera.EyePos;
            var distanceSquared = rel.LengthSquared();
            if (distanceSquared <= 0.0025f || distanceSquared > maxDistanceSquared)
                continue;

            if (!_resolver.TryResolve(uid, out var resolved))
                continue;

            if (resolved.Archetype is E3DArchetype.Wall or E3DArchetype.SmoothWall or E3DArchetype.Edge or E3DArchetype.OccluderOnly or E3DArchetype.Floor or E3DArchetype.DecalLike)
                continue;

            if (resolved.Archetype is E3DArchetype.Door or E3DArchetype.Window or E3DArchetype.Frame)
                continue;

            var face = resolved.Archetype == E3DArchetype.Table
                ? Direction.Invalid
                : GetBillboardFace(world, camera.EyePos, _transform.GetWorldRotation(xform));
            _resolver.GetVisibleLayers(uid, face, _billboardLayers, true);
            if (_billboardLayers.Count == 0)
                continue;

            var combinedBounds = _billboardLayers[0].Bounds;
            for (var i = 1; i < _billboardLayers.Count; i++)
                combinedBounds = combinedBounds.Union(_billboardLayers[i].Bounds);

            world += resolved.WorldOffset;
            rel = world - camera.EyePos;
            var forwardDepth = Vector2.Dot(rel, camera.Yaw.ToVec());
            if (forwardDepth <= 0.05f || forwardDepth > camera.MaxDistance)
                continue;

            var sideDepth = Vector2.Dot(rel, right.ToVec());
            var screenX = screenWidth / 2f + sideDepth / forwardDepth * projectionPlane;
            Box2? fixtureBounds = resolved.PreferFixtureBounds && TryGetFixtureBounds(uid, out var fixtureBox)
                ? fixtureBox
                : null;
            var spriteWidth = MathF.Max(0.12f, combinedBounds.Width * 0.95f);
            var spriteHeight = MathF.Max(0.12f, combinedBounds.Height * 0.95f);
            if (resolved.Archetype == E3DArchetype.Mob && resolved.Height > spriteHeight)
            {
                var scale = resolved.Height / MathF.Max(0.0001f, spriteHeight);
                spriteHeight *= scale;
                spriteWidth *= scale;
            }
            var fixtureWidth = fixtureBounds?.Width ?? spriteWidth;
            var fixtureHeight = fixtureBounds?.Height ?? spriteHeight;
            var worldWidth = resolved.PreferFixtureBounds && spriteWidth > 0.85f
                ? MathF.Max(0.45f, MathF.Min(spriteWidth, MathF.Max(fixtureWidth, spriteWidth * 0.72f)))
                : spriteWidth;
            var worldHeight = MathF.Max(spriteHeight, fixtureHeight);

            var projectedHeight = MathF.Max(2f, projectionPlane * worldHeight / forwardDepth);
            var projectedWidth = MathF.Max(2f, projectionPlane * worldWidth / forwardDepth);
            if (screenX + projectedWidth < 0f || screenX - projectedWidth > screenWidth)
                continue;

            var horizon = screenHeight / 2f - (float) (camera.Pitch.Degrees / 70f) * screenHeight * 0.35f;
            var groundY = horizon + projectionPlane * camera.EyeHeight / MathF.Max(0.05f, forwardDepth);
            var verticalOffset = projectionPlane * resolved.EyeOffset / MathF.Max(0.05f, forwardDepth);
            var floorAnchored = resolved.FloorAnchored || resolved.Archetype == E3DArchetype.Mob;
            var top = resolved.WallMounted
                ? groundY - projectedHeight * 1.2f - verticalOffset
                : !floorAnchored
                    ? groundY - projectedHeight * 0.5f - verticalOffset
                    : groundY - projectedHeight - verticalOffset;

            if (resolved.Archetype == E3DArchetype.DecalLike)
                top = groundY - projectedHeight * 0.5f - verticalOffset;
            var rect = UIBox2.FromDimensions(
                new Vector2(screenX - projectedWidth / 2f, top),
                new Vector2(projectedWidth, projectedHeight));

            var leftIndex = (int) Math.Clamp(MathF.Floor(rect.Left), 0, depthBuffer.Length - 1);
            var rightIndex = (int) Math.Clamp(MathF.Ceiling(rect.Right), 0, depthBuffer.Length - 1);
            var occluded = false;
            for (var i = leftIndex; i <= rightIndex && i < depthBuffer.Length; i++)
            {
                if (depthBuffer[i] > 0f &&
                    forwardDepth > depthBuffer[i] + resolved.DepthBias)
                {
                    occluded = true;
                    break;
                }
            }

            if (occluded)
                continue;

            var sortDepth = forwardDepth;
            output.Add(new FpvBillboard(
                uid,
                rect,
                forwardDepth,
                sortDepth,
                sprite.DrawDepth,
                resolved.Transparent,
                _billboardLayers[0].Texture,
                _billboardLayers[0].Color,
                combinedBounds,
                face));
        }

        output.Sort(static (a, b) =>
        {
            var depthDelta = b.SortDepth - a.SortDepth;
            if (MathF.Abs(depthDelta) > 0.05f)
                return depthDelta > 0f ? 1 : -1;

            var drawCmp = a.DrawDepth.CompareTo(b.DrawDepth);
            if (drawCmp != 0)
                return drawCmp;

            return a.Entity.CompareTo(b.Entity);
        });
        if (output.Count > camera.MaxBillboards)
            output.RemoveRange(camera.MaxBillboards, output.Count - camera.MaxBillboards);
    }

    public void CollectFloorDecals(FpvCameraState camera, float[] depthBuffer, int screenWidth, List<FpvFloorDecal> output)
    {
        output.Clear();
        if (camera.MapId == MapId.Nullspace)
            return;

        var projectionPlane = GetProjectionPlaneDistance(screenWidth, camera.FovDegrees);
        var right = camera.Yaw + Angle.FromDegrees(90f);
        var maxDistanceSquared = camera.MaxDistance * camera.MaxDistance;
        _nearbyEntities.Clear();
        _lookup.GetEntitiesInRange(camera.MapId, camera.EyePos, camera.MaxDistance + 0.75f, _nearbyEntities, LookupFlags.Uncontained | LookupFlags.Approximate);

        foreach (var uid in _nearbyEntities)
        {
            if (!TryComp(uid, out TransformComponent? xform) ||
                !TryComp(uid, out SpriteComponent? sprite) ||
                xform.MapID != camera.MapId)
            {
                continue;
            }

            if (!_resolver.IsSpriteRenderable(sprite) || !_resolver.TryResolve(uid, out var resolved) || resolved.Archetype != E3DArchetype.DecalLike)
                continue;

            var world = _transform.GetMapCoordinates(uid, xform).Position + resolved.WorldOffset;
            var face = GetBillboardFace(world, camera.EyePos, _transform.GetWorldRotation(xform));
            if (!_resolver.TryGetBillboardVisual(uid, face, out var texture, out var bounds, out var tint) || texture == null)
                continue;
            var rel = world - camera.EyePos;
            var distanceSquared = rel.LengthSquared();
            if (distanceSquared <= 0.0025f || distanceSquared > maxDistanceSquared)
                continue;

            var forwardDepth = Vector2.Dot(rel, camera.Yaw.ToVec());
            if (forwardDepth <= 0.05f || forwardDepth > camera.MaxDistance)
                continue;

            var sideDepth = Vector2.Dot(rel, right.ToVec());
            var screenX = screenWidth / 2f + sideDepth / forwardDepth * projectionPlane;
            if (screenX < -32f || screenX > screenWidth + 32f)
                continue;

            var depthIndex = (int) Math.Clamp(screenX, 0f, depthBuffer.Length - 1);
            var visibleDepth = depthBuffer.Length > 0 ? depthBuffer[depthIndex] : 0f;
            if (visibleDepth > 0f && forwardDepth > visibleDepth - 0.05f)
                continue;

            output.Add(new FpvFloorDecal(
                uid,
                texture,
                world,
                MathF.Max(0.18f, bounds.Width * 0.95f),
                MathF.Max(0.18f, bounds.Height * 0.95f),
                _transform.GetWorldRotation(xform),
                forwardDepth,
                ApplyDistanceFog(tint, forwardDepth, camera)));
        }

        output.Sort(static (a, b) => b.Distance.CompareTo(a.Distance));
    }

    public Texture? TryGetFloorTexture(TileRef tile, out UIBox2 textureRegion)
    {
        textureRegion = default;
        var def = (ContentTileDefinition) _tileDefs[tile.Tile.TypeId];
        if (def.Sprite == null)
            return null;

        var texture = _resources.GetTexture(def.Sprite.Value);
        var tileSize = texture.Height;
        var variant = Math.Clamp(tile.Tile.Variant, 0, Math.Max(0, def.Variants - 1));
        textureRegion = new UIBox2(variant * tileSize, 0, (variant + 1) * tileSize, tileSize);
        return texture;
    }

    public bool TryGetTile(MapCoordinates coordinates, out TileRef tile)
    {
        tile = default;

        if (!_mapManager.TryFindGridAt(coordinates, out var gridUid, out var grid))
            return false;

        return _map.TryGetTileRef(gridUid, grid, coordinates.Position, out tile);
    }

    public Color ApplyDistanceFog(Color color, float distance, FpvCameraState camera)
    {
        if (camera.LightingMode == FirstPersonLightingMode.Unlit)
            return color;

        var fogColor = camera.LightingMode == FirstPersonLightingMode.Ambient
            ? new Color(24, 24, 28)
            : new Color(18, 20, 28);
        var factor = Math.Clamp(distance / MathF.Max(0.001f, camera.MaxDistance), 0f, 1f);
        factor = camera.LightingMode == FirstPersonLightingMode.Ambient ? factor * 0.65f : factor * 0.85f;
        return new Color(Vector4.Lerp(color.RGBA, fogColor.RGBA, factor));
    }

    public Angle GetRayAngle(FpvCameraState camera, float xPx, int widthPx)
    {
        var t = (xPx + 0.5f) / widthPx;
        var radians = MathF.PI * (camera.FovDegrees / 180f);
        return camera.Yaw + new Angle((t - 0.5f) * radians);
    }

    public float GetProjectionPlaneDistance(float screenWidth, float fovDegrees)
    {
        var halfFov = MathF.PI * (fovDegrees / 180f) / 2f;
        return screenWidth / MathF.Max(0.001f, 2f * MathF.Tan(halfFov));
    }

    private bool TryGetFixtureBounds(EntityUid uid, out Box2 bounds)
    {
        bounds = default;
        if (!TryComp(uid, out FixturesComponent? fixtures))
            return false;

        var any = false;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            var aabb = fixture.Shape.ComputeAABB(new PhysicsTransform(Vector2.Zero, Angle.Zero), 0);
            bounds = any ? bounds.Union(aabb) : aabb;
            any = true;
        }

        return any;
    }

    private bool TryCastGridRay(FpvCameraState camera, Vector2 worldDir, bool skipTransparent, out FpvRayHit hit)
    {
        hit = default;

        if (!_mapManager.TryFindGridAt(new MapCoordinates(camera.EyePos, camera.MapId), out var gridUid, out var grid))
            return false;

        var localOrigin = _map.WorldToLocal(gridUid, grid, camera.EyePos);
        var localAhead = _map.WorldToLocal(gridUid, grid, camera.EyePos + worldDir);
        var localDir = localAhead - localOrigin;
        if (localDir.LengthSquared() <= 0.000001f)
            return false;

        localDir = Vector2.Normalize(localDir);
        var tileSize = grid.TileSize;
        var tileX = (int) MathF.Floor(localOrigin.X / tileSize);
        var tileY = (int) MathF.Floor(localOrigin.Y / tileSize);
        var stepX = localDir.X >= 0f ? 1 : -1;
        var stepY = localDir.Y >= 0f ? 1 : -1;
        var deltaDistX = localDir.X == 0f ? float.PositiveInfinity : MathF.Abs(tileSize / localDir.X);
        var deltaDistY = localDir.Y == 0f ? float.PositiveInfinity : MathF.Abs(tileSize / localDir.Y);
        var nextBoundaryX = (tileX + (stepX > 0 ? 1 : 0)) * tileSize;
        var nextBoundaryY = (tileY + (stepY > 0 ? 1 : 0)) * tileSize;
        var sideDistX = localDir.X == 0f ? float.PositiveInfinity : MathF.Abs((nextBoundaryX - localOrigin.X) / localDir.X);
        var sideDistY = localDir.Y == 0f ? float.PositiveInfinity : MathF.Abs((nextBoundaryY - localOrigin.Y) / localDir.Y);

        while (true)
        {
            var verticalSide = sideDistX < sideDistY;
            float hitDistance;

            if (verticalSide)
            {
                hitDistance = sideDistX;
                sideDistX += deltaDistX;
                tileX += stepX;
            }
            else
            {
                hitDistance = sideDistY;
                sideDistY += deltaDistY;
                tileY += stepY;
            }

            if (hitDistance > camera.MaxDistance)
                return false;

            var tile = new Vector2i(tileX, tileY);
            if (!_map.TryGetTileRef(gridUid, grid, tile, out _))
                return false;

            if (!TryGetSurfaceInTile(gridUid, grid, tile, skipTransparent, out var wallEntity))
                continue;

            var localHit = localOrigin + localDir * hitDistance;
            var worldHit = _map.LocalToWorld(gridUid, grid, localHit);
            var face = verticalSide
                ? (stepX > 0 ? Direction.West : Direction.East)
                : (stepY > 0 ? Direction.South : Direction.North);
            hit = new FpvRayHit(wallEntity, worldHit, hitDistance, verticalSide, face);
            return true;
        }
    }

    private bool TryGetSurfaceInTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile, bool skipTransparent, out EntityUid wallEntity)
    {
        wallEntity = default;
        var bestPriority = 0;
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (!_resolver.TryResolve(ent.Value, out var resolved))
                continue;

            if (skipTransparent && resolved.Transparent)
                continue;

            if (!resolved.BlocksVision && resolved.Archetype is not E3DArchetype.Window and not E3DArchetype.Frame and not E3DArchetype.Door)
                continue;

            var priority = resolved.Archetype switch
            {
                E3DArchetype.Door => _resolver.IsClosedDoor(ent.Value) ? 50 : 0,
                E3DArchetype.Window => 40,
                E3DArchetype.Frame => 35,
                E3DArchetype.Wall or E3DArchetype.SmoothWall or E3DArchetype.Edge or E3DArchetype.OccluderOnly => 30,
                _ => 0
            };

            if (priority <= bestPriority)
                continue;

            bestPriority = priority;
            wallEntity = ent.Value;
        }

        return bestPriority > 0;
    }

    private bool TryFindInteractableEntity(FpvCameraState camera, float maxDistance, out EntityUid? entity, out Vector2 coordinates)
    {
        entity = null;
        coordinates = camera.EyePos + camera.Yaw.ToVec() * maxDistance;
        EntityUid? best = null;
        var bestDistance = maxDistance;

        _nearbyEntities.Clear();
        _lookup.GetEntitiesInRange(camera.MapId, camera.EyePos, maxDistance + 0.75f, _nearbyEntities, LookupFlags.Uncontained | LookupFlags.Approximate);
        foreach (var uid in _nearbyEntities)
        {
            if (!TryComp(uid, out TransformComponent? xform))
                continue;

            if (xform.MapID != camera.MapId || !_resolver.TryResolve(uid, out var resolved))
                continue;

            if (resolved.Archetype is E3DArchetype.GasOverlay or E3DArchetype.DecalLike or E3DArchetype.Floor)
                continue;

            var world = _transform.GetMapCoordinates(uid, xform).Position + resolved.WorldOffset;
            var rel = world - camera.EyePos;
            var depth = Vector2.Dot(rel, camera.Yaw.ToVec());
            if (depth <= 0.05f || depth > bestDistance)
                continue;

            var side = MathF.Abs(Vector2.Dot(rel, (camera.Yaw + Angle.FromDegrees(90)).ToVec()));
            var width = MathF.Max(0.2f, resolved.Width * 0.5f);
            if (side > width)
                continue;

            best = uid;
            bestDistance = depth;
            coordinates = world;
        }

        entity = best;
        return entity != null;
    }

    private static Direction GetBillboardFace(Vector2 world, Vector2 eyePos, Angle worldRotation)
    {
        var toCamera = eyePos - world;
        if (toCamera.LengthSquared() <= 0.0001f)
            return Direction.Invalid;

        var angleToCamera = Angle.FromWorldVec(toCamera);
        var relative = angleToCamera - worldRotation;
        return relative.GetDir();
    }
}
