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
    private const float DdaSideEpsilon = 0.0001f;

    [Dependency] private readonly E3DArchetypeResolverSystem _resolver = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _nearbyEntities = new();
    private readonly List<EntityUid> _cachedGridUids = new();
    private const uint TileSurfaceCacheFrameTtl = 3;
    private const int TileSurfaceCacheHardLimit = 16_384;
    private readonly Dictionary<(EntityUid GridUid, Vector2i Tile, bool SkipTransparent), TileSurfaceCacheEntry> _tileSurfaceCache = new();
    private readonly object _tileSurfaceCacheLock = new();
    private readonly List<FpvVisualLayer> _billboardLayers = new();
    private MapId _cachedNearbyMap = MapId.Nullspace;
    private Vector2 _cachedNearbyEye;
    private float _cachedNearbyRadius = -1f;
    private MapId _cachedGridMap = MapId.Nullspace;
    private uint _frameId;

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

    public void BeginFrame(FpvCameraState camera)
    {
        _frameId++;
        EnsureGridCache(camera.MapId);
        _cachedNearbyRadius = -1f;
        if ((_frameId & 63) == 0)
            PruneTileSurfaceCache();
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
        EnsureNearbyEntities(camera.MapId, camera.EyePos, camera.MaxDistance + 0.75f);

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

            var world = GetRenderableWorldPosition(uid, xform, sprite, Vector2.Zero);
            var rel = world - camera.EyePos;
            var distanceSquared = rel.LengthSquared();
            if (distanceSquared <= 0.0025f || distanceSquared > maxDistanceSquared)
                continue;

            if (!_resolver.TryResolve(uid, out var resolved))
                continue;

            if (resolved.Archetype is E3DArchetype.Wall or E3DArchetype.SmoothWall or E3DArchetype.Edge or E3DArchetype.OccluderOnly or E3DArchetype.Floor or E3DArchetype.DecalLike or E3DArchetype.Grille)
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

            world = GetRenderableWorldPosition(uid, xform, sprite, resolved.WorldOffset);
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

            if (!TryGetVisibleBillboardSpan(rect, forwardDepth, resolved.DepthBias, depthBuffer, out var visibleLeft, out var visibleRight))
                continue;

            var sortDepth = forwardDepth;
            output.Add(new FpvBillboard(
                uid,
                rect,
                visibleLeft,
                visibleRight,
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
            var depthDelta = a.SortDepth.CompareTo(b.SortDepth);
            if (MathF.Abs(depthDelta) > 0.05f)
                return depthDelta;

            var drawCmp = a.DrawDepth.CompareTo(b.DrawDepth);
            if (drawCmp != 0)
                return drawCmp;

            return a.Entity.CompareTo(b.Entity);
        });
        if (camera.MaxBillboards > 0 && output.Count > camera.MaxBillboards)
            output.RemoveRange(camera.MaxBillboards, output.Count - camera.MaxBillboards);
    }

    public void GetOrderedTileSurfaceEntities(FpvRayHit hit, List<EntityUid> output)
    {
        output.Clear();
        if (hit.HitGridUid == EntityUid.Invalid || !TryComp(hit.HitGridUid, out MapGridComponent? grid))
        {
            output.Add(hit.HitEntity);
            return;
        }

        var scored = new List<(EntityUid Uid, int Priority)>();
        var anchored = _map.GetAnchoredEntitiesEnumerator(hit.HitGridUid, grid, hit.GridTile);
        while (anchored.MoveNext(out var ent))
        {
            if (!_resolver.TryResolve(ent.Value, out var resolved))
                continue;

            if (!resolved.BlocksVision && resolved.Archetype is not E3DArchetype.Window and not E3DArchetype.Frame and not E3DArchetype.Door and not E3DArchetype.Grille)
                continue;

            var p = SurfaceRayPriority(resolved.Archetype, ent.Value);
            if (p <= 0)
                continue;

            scored.Add((ent.Value, p));
        }

        scored.Sort(static (a, b) => a.Priority.CompareTo(b.Priority));
        foreach (var entry in scored)
            output.Add(entry.Uid);

        if (output.Count == 0)
            output.Add(hit.HitEntity);
    }

    private int SurfaceRayPriority(E3DArchetype archetype, EntityUid uid)
    {
        return archetype switch
        {
            E3DArchetype.Door => _resolver.IsClosedDoor(uid) ? 50 : 0,
            E3DArchetype.Window => 40,
            E3DArchetype.Grille => 38,
            E3DArchetype.Frame => 35,
            E3DArchetype.Wall or E3DArchetype.SmoothWall or E3DArchetype.Edge or E3DArchetype.OccluderOnly => 30,
            _ => 0
        };
    }

    public void CollectFloorDecals(FpvCameraState camera, float[] depthBuffer, int screenWidth, List<FpvFloorDecal> output)
    {
        output.Clear();
        if (camera.MapId == MapId.Nullspace)
            return;

        var projectionPlane = GetProjectionPlaneDistance(screenWidth, camera.FovDegrees);
        var right = camera.Yaw + Angle.FromDegrees(90f);
        var maxDistanceSquared = camera.MaxDistance * camera.MaxDistance;
        EnsureNearbyEntities(camera.MapId, camera.EyePos, camera.MaxDistance + 0.75f);

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

            var world = GetRenderableWorldPosition(uid, xform, sprite, resolved.WorldOffset);
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
        if (widthPx <= 0)
            return camera.Yaw;

        var projectionPlane = GetProjectionPlaneDistance(widthPx, camera.FovDegrees);
        var centeredX = xPx - widthPx / 2f;
        return camera.Yaw + new Angle(MathF.Atan2(centeredX, projectionPlane));
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

    public Vector2 GetGridLocalHitPos(FpvRayHit hit)
    {
        if (hit.HitGridUid == EntityUid.Invalid)
            return hit.HitPos;

        return hit.GridLocalHitPos;
    }

    private bool TryCastGridRay(FpvCameraState camera, Vector2 worldDir, bool skipTransparent, out FpvRayHit hit)
    {
        hit = default;
        if (worldDir.LengthSquared() <= 1e-8f)
            return false;

        var dirUnit = Vector2.Normalize(worldDir);
        var bestDist = float.PositiveInfinity;
        var found = false;

        EnsureGridCache(camera.MapId);
        foreach (var gridUid in _cachedGridUids)
        {
            if (!TryComp(gridUid, out MapGridComponent? grid) || !TryCastGridRayOnGrid(gridUid, grid, camera, dirUnit, skipTransparent, out var candidate))
                continue;

            if (candidate.Distance >= bestDist)
                continue;

            bestDist = candidate.Distance;
            hit = candidate;
            found = true;
        }

        return found;
    }

    private bool TryCastGridRayOnGrid(
        EntityUid gridUid,
        MapGridComponent grid,
        FpvCameraState camera,
        Vector2 worldDirUnit,
        bool skipTransparent,
        out FpvRayHit hit)
    {
        hit = default;

        var worldRot = _transform.GetWorldRotation(gridUid);
        float tEnter;

        var localOrigin = _map.WorldToLocal(gridUid, grid, camera.EyePos);
        var localAhead = _map.WorldToLocal(gridUid, grid, camera.EyePos + worldDirUnit);
        var localDir = localAhead - localOrigin;
        if (localDir.LengthSquared() <= 0.000001f)
            return false;

        localDir = Vector2.Normalize(localDir);

        if (!TryRaySegmentIntersectAabb(localOrigin, localDir, camera.MaxDistance, grid.LocalAABB, out tEnter, out var tExit))
            return false;

        var tStart = MathF.Max(0f, tEnter);
        if (tStart > tExit)
            return false;

        var rayStart = localOrigin + localDir * tStart;
        var tileSize = grid.TileSize;
        var tileX = (int) MathF.Floor(rayStart.X / tileSize);
        var tileY = (int) MathF.Floor(rayStart.Y / tileSize);
        var stepX = localDir.X >= 0f ? 1 : -1;
        var stepY = localDir.Y >= 0f ? 1 : -1;
        var deltaDistX = localDir.X == 0f ? float.PositiveInfinity : MathF.Abs(tileSize / localDir.X);
        var deltaDistY = localDir.Y == 0f ? float.PositiveInfinity : MathF.Abs(tileSize / localDir.Y);
        var nextBoundaryX = (tileX + (stepX > 0 ? 1 : 0)) * tileSize;
        var nextBoundaryY = (tileY + (stepY > 0 ? 1 : 0)) * tileSize;
        var sideDistX = localDir.X == 0f ? float.PositiveInfinity : MathF.Abs((nextBoundaryX - rayStart.X) / localDir.X);
        var sideDistY = localDir.Y == 0f ? float.PositiveInfinity : MathF.Abs((nextBoundaryY - rayStart.Y) / localDir.Y);

        while (true)
        {
            var sideDelta = sideDistX - sideDistY;
            var verticalSide = sideDelta < -DdaSideEpsilon || (MathF.Abs(sideDelta) <= DdaSideEpsilon && stepX > 0);
            float distFromRayStart;

            if (verticalSide)
            {
                distFromRayStart = sideDistX;
                sideDistX += deltaDistX;
                tileX += stepX;
            }
            else
            {
                distFromRayStart = sideDistY;
                sideDistY += deltaDistY;
                tileY += stepY;
            }

            var totalDist = tStart + distFromRayStart;
            if (totalDist > camera.MaxDistance)
                return false;

            var tile = new Vector2i(tileX, tileY);
            if (!_map.TryGetTileRef(gridUid, grid, tile, out _))
                continue;

            if (!TryGetSurfaceInTile(gridUid, grid, tile, skipTransparent, out var wallEntity))
                continue;

            var localHit = localOrigin + localDir * totalDist;
            var worldHit = _map.LocalToWorld(gridUid, grid, localHit);
            var gridFace = verticalSide
                ? (stepX > 0 ? Direction.West : Direction.East)
                : (stepY > 0 ? Direction.South : Direction.North);
            var worldNormal = worldRot.RotateVec(gridFace.ToVec());
            var worldFace = Angle.FromWorldVec(worldNormal).GetCardinalDir();
            hit = new FpvRayHit(wallEntity, worldHit, totalDist, verticalSide, worldFace, gridUid, new Vector2i(tileX, tileY), localHit, gridFace, worldNormal);
            return true;
        }
    }

    private static bool TryRaySegmentIntersectAabb(Vector2 origin, Vector2 dir, float maxT, Box2 box, out float tEnter, out float tExit)
    {
        tEnter = 0f;
        tExit = maxT;

        if (dir.X is > -1e-6f and < 1e-6f)
        {
            if (origin.X < box.Left || origin.X > box.Right)
                return false;
        }
        else
        {
            var inv = 1f / dir.X;
            var t1 = (box.Left - origin.X) * inv;
            var t2 = (box.Right - origin.X) * inv;
            if (t1 > t2)
                (t1, t2) = (t2, t1);

            tEnter = MathF.Max(tEnter, t1);
            tExit = MathF.Min(tExit, t2);
        }

        if (dir.Y is > -1e-6f and < 1e-6f)
        {
            if (origin.Y < box.Bottom || origin.Y > box.Top)
                return false;
        }
        else
        {
            var inv = 1f / dir.Y;
            var t1 = (box.Bottom - origin.Y) * inv;
            var t2 = (box.Top - origin.Y) * inv;
            if (t1 > t2)
                (t1, t2) = (t2, t1);

            tEnter = MathF.Max(tEnter, t1);
            tExit = MathF.Min(tExit, t2);
        }

        return tEnter <= tExit && tExit >= 0f;
    }

    private bool TryGetSurfaceInTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile, bool skipTransparent, out EntityUid wallEntity)
    {
        wallEntity = default;
        TileSurfaceCacheEntry cached;
        lock (_tileSurfaceCacheLock)
        {
            if (_tileSurfaceCache.TryGetValue((gridUid, tile, skipTransparent), out cached))
            {
                var frameAge = _frameId - cached.FrameId;
                if (frameAge <= TileSurfaceCacheFrameTtl)
                {
                    wallEntity = cached.Entity ?? default;
                    return cached.Entity != null;
                }
            }
        }

        var key = (gridUid, tile, skipTransparent);

        var bestPriority = 0;
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (!_resolver.TryResolve(ent.Value, out var resolved))
                continue;

            if (skipTransparent && resolved.Transparent)
                continue;

            if (!resolved.BlocksVision && resolved.Archetype is not E3DArchetype.Window and not E3DArchetype.Frame and not E3DArchetype.Door and not E3DArchetype.Grille)
                continue;

            var priority = SurfaceRayPriority(resolved.Archetype, ent.Value);

            if (priority <= bestPriority)
                continue;

            bestPriority = priority;
            wallEntity = ent.Value;
        }

        var found = bestPriority > 0;
        lock (_tileSurfaceCacheLock)
        {
            _tileSurfaceCache[key] = new TileSurfaceCacheEntry(found ? wallEntity : null, _frameId);
        }
        return found;
    }

    private void PruneTileSurfaceCache()
    {
        lock (_tileSurfaceCacheLock)
        {
            if (_tileSurfaceCache.Count <= TileSurfaceCacheHardLimit)
                return;

            var remove = new List<(EntityUid GridUid, Vector2i Tile, bool SkipTransparent)>();
            foreach (var (key, value) in _tileSurfaceCache)
            {
                if (_frameId - value.FrameId > TileSurfaceCacheFrameTtl)
                    remove.Add(key);
            }

            foreach (var key in remove)
            {
                _tileSurfaceCache.Remove(key);
            }
        }
    }

    private bool TryFindInteractableEntity(FpvCameraState camera, float maxDistance, out EntityUid? entity, out Vector2 coordinates)
    {
        entity = null;
        coordinates = camera.EyePos + camera.Yaw.ToVec() * maxDistance;
        EntityUid? best = null;
        var bestDistance = maxDistance;

        EnsureNearbyEntities(camera.MapId, camera.EyePos, maxDistance + 0.75f);
        foreach (var uid in _nearbyEntities)
        {
            if (!TryComp(uid, out TransformComponent? xform))
                continue;

            if (xform.MapID != camera.MapId || !_resolver.TryResolve(uid, out var resolved))
                continue;

            if (resolved.Archetype is E3DArchetype.GasOverlay or E3DArchetype.DecalLike or E3DArchetype.Floor)
                continue;

            var world = GetRenderableWorldPosition(uid, xform, CompOrNull<SpriteComponent>(uid), resolved.WorldOffset);
            var rel = world - camera.EyePos;
            var depth = Vector2.Dot(rel, camera.Yaw.ToVec());
            if (depth <= 0.05f || depth > bestDistance)
                continue;

            var side = MathF.Abs(Vector2.Dot(rel, (camera.Yaw + Angle.FromDegrees(90)).ToVec()));
            var width = MathF.Max(0.35f, resolved.Width);
            if (side > width)
                continue;

            best = uid;
            bestDistance = depth;
            coordinates = world;
        }

        entity = best;
        return entity != null;
    }

    private Vector2 GetRenderableWorldPosition(EntityUid uid, TransformComponent xform, SpriteComponent? sprite, Vector2 worldOffset)
    {
        var world = sprite != null && _resolver.IsSpriteRenderable(sprite)
            ? _sprites.GetSpriteWorldPosition((uid, sprite, xform))
            : _transform.GetMapCoordinates(uid, xform).Position;

        return world + worldOffset;
    }

    private static bool TryGetVisibleBillboardSpan(UIBox2 rect, float depth, float depthBias, float[] depthBuffer, out float visibleLeft, out float visibleRight)
    {
        visibleLeft = rect.Left;
        visibleRight = rect.Right;

        if (rect.Width <= 0f)
            return false;

        if (depthBuffer.Length == 0)
            return true;

        var leftIndex = (int) Math.Clamp(MathF.Floor(rect.Left), 0, depthBuffer.Length - 1);
        var rightIndex = (int) Math.Clamp(MathF.Ceiling(rect.Right) - 1f, 0, depthBuffer.Length - 1);
        var firstVisible = -1;
        var lastVisible = -1;

        for (var i = leftIndex; i <= rightIndex; i++)
        {
            var occluderDepth = depthBuffer[i];
            if (occluderDepth > 0f && depth > occluderDepth + depthBias)
                continue;

            if (firstVisible == -1)
                firstVisible = i;

            lastVisible = i;
        }

        if (firstVisible == -1 || lastVisible == -1)
            return false;

        visibleLeft = MathF.Max(rect.Left, firstVisible);
        visibleRight = MathF.Min(rect.Right, lastVisible + 1f);
        return visibleRight - visibleLeft > 0.5f;
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

    private void EnsureNearbyEntities(MapId mapId, Vector2 eyePos, float radius)
    {
        if (_cachedNearbyMap == mapId &&
            _cachedNearbyRadius >= radius &&
            (eyePos - _cachedNearbyEye).LengthSquared() <= 0.0025f)
        {
            return;
        }

        _nearbyEntities.Clear();
        _lookup.GetEntitiesInRange(mapId, eyePos, radius, _nearbyEntities, LookupFlags.Uncontained | LookupFlags.Approximate);
        _cachedNearbyMap = mapId;
        _cachedNearbyEye = eyePos;
        _cachedNearbyRadius = radius;
    }

    private void EnsureGridCache(MapId mapId)
    {
        if (_cachedGridMap == mapId)
            return;

        _cachedGridUids.Clear();
        foreach (var gridEnt in _mapManager.GetAllGrids(mapId))
        {
            _cachedGridUids.Add(gridEnt.Owner);
        }

        _cachedGridMap = mapId;
        lock (_tileSurfaceCacheLock)
        {
            _tileSurfaceCache.Clear();
        }
    }

    private readonly record struct TileSurfaceCacheEntry(EntityUid? Entity, uint FrameId);
}
