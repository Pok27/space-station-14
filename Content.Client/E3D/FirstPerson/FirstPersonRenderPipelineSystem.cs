using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Parallax;
using Content.Client.Parallax.Managers;
using Content.Shared.E3D;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Threading;
using Robust.Shared.Timing;

namespace Content.Client.E3D.FirstPerson;

public sealed class FirstPersonRenderPipelineSystem : EntitySystem
{
    private const float MinSurfaceRenderDistance = 0.05f;
    private const float MinCorrectedDistance = 0.05f;
    private const float MaxSurfaceScreenHeightMultiplier = 3.5f;
    private const float MinLayerBoundsSizeSquared = 0.0001f;
    private const float MinRelativeLengthSquared = 0.0001f;
    private const float MinNonZero = 0.0001f;
    private const float HorizonEpsilon = 0.01f;
    private const float OcclusionEpsilon = 0.01f;
    private const float FloorFadeDistance = 1.25f;
    private const float FloorDecalBinSize = 1f;
    private const float SurfaceVerticalShade = 0.92f;
    private const float TransparentSurfaceAlpha = 0.6f;
    private const float TransparentBillboardAlpha = 0.75f;
    private const float CrosshairAlpha = 0.7f;
    private const int CrosshairHalfLengthPx = 6;
    private const int CrosshairHalfThicknessPx = 1;
    private static readonly Color BackgroundTopColor = new(0, 0, 0);
    private static readonly Color BackgroundBottomColor = new(0, 0, 0);

    [Dependency] private readonly FirstPersonFloorCacheSystem _floorCache = default!;
    [Dependency] private readonly FirstPersonInteractionSystem _interaction = default!;
    [Dependency] private readonly FirstPersonSceneBuilderSystem _sceneBuilder = default!;
    [Dependency] private readonly E3DArchetypeResolverSystem _resolver = default!;
    [Dependency] private readonly IParallelManager _parallel = default!;
    [Dependency] private readonly ParallaxSystem _parallax = default!;
    [Dependency] private readonly IParallaxManager _parallaxManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly List<FpvBillboard> _billboards = new();
    private readonly List<FpvVisualLayer> _billboardLayers = new();
    private readonly List<FpvFloorDecal> _floorDecals = new();
    private readonly Dictionary<Vector2i, List<int>> _floorDecalBins = new();
    private readonly List<FpvVisualLayer> _surfaceLayers = new();
    private readonly List<TransparentSurfaceDraw> _transparentSurfaces = new();
    private readonly List<AlphaDraw> _alphaDraws = new();
    private readonly List<EntityUid> _tileSurfaceEntities = new();
    private float[] _depthBuffer = Array.Empty<float>();
    private int[] _surfaceBandLeft = Array.Empty<int>();
    private int[] _surfaceBandWidth = Array.Empty<int>();
    private Angle[] _surfaceRayAngle = Array.Empty<Angle>();
    private bool[] _surfaceHasHit = Array.Empty<bool>();
    private bool[] _surfaceHasOpaque = Array.Empty<bool>();
    private FpvRayHit[] _surfaceHit = Array.Empty<FpvRayHit>();
    private FpvRayHit[] _surfaceOpaqueHit = Array.Empty<FpvRayHit>();
    private ResolvedE3DRenderable[] _surfaceResolved = Array.Empty<ResolvedE3DRenderable>();
    private ResolvedE3DRenderable[] _surfaceOpaqueResolved = Array.Empty<ResolvedE3DRenderable>();
    private float[] _surfaceCorrectedDist = Array.Empty<float>();
    private float[] _surfaceRenderDist = Array.Empty<float>();
    private float[] _surfaceOpaqueCorrectedDist = Array.Empty<float>();
    private float[] _surfaceOpaqueRenderDist = Array.Empty<float>();
    private float[] _surfaceOccluderDist = Array.Empty<float>();

    public void DrawFrame(FirstPersonViewControl control, DrawingHandleScreen handle, FpvCameraState camera, float horizon)
    {
        if (camera.MapId == MapId.Nullspace)
        {
            _interaction.Clear();
            return;
        }

        var sizePx = (Vector2) control.PixelSize;
        if (sizePx.X <= 1 || sizePx.Y <= 1)
        {
            _interaction.Clear();
            return;
        }

        var width = control.PixelSize.X;
        var height = sizePx.Y;
        EnsureDepthBuffer(width);
        _sceneBuilder.BeginFrame(camera);

        DrawBackground(handle, sizePx, horizon, camera);
        DrawSurfaces(handle, camera, width, height, horizon);
        _sceneBuilder.CollectFloorDecals(camera, _depthBuffer, width, _floorDecals);
        BuildFloorDecalBins();
        DrawFloor(handle, camera, width, height, horizon);
        DrawAlphaPass(handle, camera, width, (int) height);
        PublishInteractionHit(camera);

        if (control.DrawCrosshair)
            DrawCrosshairGlyph(handle, sizePx.X / 2f, sizePx.Y / 2f);
    }

    public MapCoordinates PixelToMap(FirstPersonViewControl control, FpvCameraState camera, Vector2 point)
    {
        var localX = point.X - control.GlobalPixelPosition.X;
        var width = control.PixelSize.X;
        if (camera.MapId == MapId.Nullspace || width <= 1)
            return default;

        localX = Math.Clamp(localX, 0, width - 1);
        if (_sceneBuilder.TryCastSurfaceRay(camera, localX, width, out var hit))
            return new MapCoordinates(hit.HitPos, camera.MapId);

        var angle = _sceneBuilder.GetRayAngle(camera, localX, width);
        return new MapCoordinates(camera.EyePos + angle.ToVec() * camera.MaxDistance, camera.MapId);
    }

    public Vector2 WorldToScreen(FirstPersonViewControl control, FpvCameraState camera, Vector2 map)
    {
        var rel = map - camera.EyePos;
        if (rel.LengthSquared() <= MinRelativeLengthSquared)
            return control.GlobalPixelPosition + (Vector2) control.PixelSize / 2f;

        var forwardDepth = Vector2.Dot(rel, camera.Yaw.ToVec());
        var sideDepth = Vector2.Dot(rel, (camera.Yaw + Angle.FromDegrees(90f)).ToVec());
        var projectionPlane = _sceneBuilder.GetProjectionPlaneDistance(control.PixelSize.X, camera.FovDegrees);
        var x = control.PixelSize.X / 2f + sideDepth / MathF.Max(MinNonZero, forwardDepth) * projectionPlane;
        var y = control.PixelSize.Y / 2f;
        return new Vector2(x, y) + control.GlobalPixelPosition;
    }

    private void DrawBackground(DrawingHandleScreen handle, Vector2 sizePx, float horizon, FpvCameraState camera)
    {
        DrawParallaxBackground(handle, sizePx, camera);
        var topHeight = Math.Clamp(horizon, 0f, sizePx.Y);
        var bottomHeight = Math.Max(0f, sizePx.Y - topHeight);

        handle.DrawRect(UIBox2.FromDimensions(Vector2.Zero, new Vector2(sizePx.X, topHeight)), BackgroundTopColor);
        handle.DrawRect(
            UIBox2.FromDimensions(new Vector2(0f, topHeight), new Vector2(sizePx.X, bottomHeight)),
            BackgroundBottomColor.WithAlpha(0.35f));
    }

    private void DrawFloor(DrawingHandleScreen handle, FpvCameraState camera, int width, float height, float horizon)
    {
        if (!camera.FloorEnabled || !camera.EnableFloorPass)
            return;

        var projectionPlane = _sceneBuilder.GetProjectionPlaneDistance(width, camera.FovDegrees);
        var halfFov = MathF.PI * (camera.FovDegrees / 180f) / 2f;
        var dir = camera.Yaw.ToVec();
        var plane = (camera.Yaw + Angle.FromDegrees(90f)).ToVec() * MathF.Tan(halfFov);
        var rayLeft = dir - plane;
        var rayRight = dir + plane;
        var invWidth = 1f / Math.Max(1, width);
        var maxFloorDistance = camera.MaxDistance + FloorFadeDistance;
        var floorLookup = default(FirstPersonFloorCacheSystem.FloorSampleLookup);

        for (var y = Math.Max(0, (int) MathF.Ceiling(horizon)); y < height;)
        {
            var p = y - horizon;
            if (p <= HorizonEpsilon)
            {
                y += 1;
                continue;
            }

            var rowDistance = camera.EyeHeight * projectionPlane / p;
            if (rowDistance > maxFloorDistance)
            {
                y += 4;
                continue;
            }

            var yStep = 1;

            var alpha = rowDistance <= camera.MaxDistance
                ? 1f
                : Math.Clamp(1f - (rowDistance - camera.MaxDistance) / FloorFadeDistance, 0f, 1f);

            if (alpha <= 0f)
            {
                y += yStep;
                continue;
            }

            var stepVec = rowDistance * (rayRight - rayLeft) * invWidth;
            var world = camera.EyePos + rayLeft * rowDistance;
            var hasSpan = false;
            var span = default(FloorSpanState);

            for (var x = 0; x < width; x++)
            {
                var minDepth = GetConservativeDepth(x, width);
                if (minDepth > 0f && rowDistance >= minDepth - OcclusionEpsilon)
                {
                    if (hasSpan)
                    {
                        FlushFloorSpan(handle, span, y, yStep);
                        hasSpan = false;
                    }

                    world += stepVec;
                    continue;
                }

                if (_floorCache.TryGetFloorSample(camera.MapId, world, ref floorLookup, out var floorSample))
                {
                    var sample = GetFloorSampleRegion(floorSample);
                    var color = _sceneBuilder.ApplyDistanceFog(Color.White, rowDistance, camera).WithAlpha(alpha);
                    var hasDecal = TryGetFloorDecalSample(world, out var decalTexture, out var decalRegion, out var decalColor);
                    if (hasDecal)
                        decalColor = decalColor.WithAlpha(decalColor.A * alpha);

                    var point = new FloorDrawPoint(
                        floorSample.Texture,
                        sample,
                        color,
                        hasDecal,
                        decalTexture,
                        decalRegion,
                        decalColor);
                    if (hasSpan && span.CanExtend(point))
                    {
                        span.Width++;
                    }
                    else
                    {
                        if (hasSpan)
                            FlushFloorSpan(handle, span, y, yStep);

                        span = new FloorSpanState(x, 1, point);
                        hasSpan = true;
                    }
                }
                else if (hasSpan)
                {
                    FlushFloorSpan(handle, span, y, yStep);
                    hasSpan = false;
                }

                world += stepVec;
            }

            if (hasSpan)
                FlushFloorSpan(handle, span, y, yStep);

            y += yStep;
        }
    }

    private static UIBox2 GetFloorSampleRegion(FpvFloorSample floorSample)
    {
        var variantRegion = floorSample.TextureRegion;
        var tilePixels = variantRegion.Width;
        var texX = MathF.Floor(variantRegion.Left + floorSample.FracX * tilePixels);
        var texY = MathF.Floor(variantRegion.Top + (1f - floorSample.FracY) * variantRegion.Height);
        texX = Math.Clamp(texX, variantRegion.Left, variantRegion.Right - 1f);
        texY = Math.Clamp(texY, variantRegion.Top, variantRegion.Bottom - 1f);
        return new UIBox2(texX, texY, texX + 1f, texY + 1f);
    }

    private float GetConservativeDepth(int x, int width)
    {
        var minDepth = _depthBuffer[x];
        if (x > 0)
            minDepth = MathF.Min(minDepth, _depthBuffer[x - 1]);
        if (x + 1 < width)
            minDepth = MathF.Min(minDepth, _depthBuffer[x + 1]);
        return minDepth;
    }

    private static void FlushFloorSpan(DrawingHandleScreen handle, FloorSpanState span, int y, int yStep)
    {
        var rect = UIBox2.FromDimensions(new Vector2(span.X, y), new Vector2(span.Width, yStep));
        handle.DrawTextureRectRegion(span.Point.Texture, rect, span.Point.Region, span.Point.Color);
        if (span.Point.HasDecal && span.Point.DecalTexture != null && span.Point.DecalRegion.HasValue)
            handle.DrawTextureRectRegion(span.Point.DecalTexture, rect, span.Point.DecalRegion.Value, span.Point.DecalColor);
    }

    private void DrawParallaxBackground(DrawingHandleScreen handle, Vector2 sizePx, FpvCameraState camera)
    {
        var layers = _parallax.GetParallaxLayers(camera.MapId);
        if (layers.Length == 0)
            return;

        var ppm = EyeManager.PixelsPerMeter;
        var time = (float) _timing.RealTime.TotalSeconds;
        foreach (var layer in layers)
        {
            var texture = layer.Texture;
            var scale = layer.Config.Scale;
            var size = texture.Size / ppm * new Vector2(MathF.Max(0.01f, scale.X), MathF.Max(0.01f, scale.Y));
            var home = layer.Config.WorldHomePosition + _parallaxManager.ParallaxAnchor;
            var scrolled = layer.Config.Scrolling * time;

            var originBL = (camera.EyePos - home) * layer.Config.Slowness + scrolled;
            originBL += home;
            originBL += layer.Config.WorldAdjustPosition;
            originBL -= size / 2f;

            var sizePxLayer = new Vector2(
                MathF.Max(1f, size.X * ppm),
                MathF.Max(1f, size.Y * ppm));
            var originPx = new Vector2(originBL.X * ppm, -originBL.Y * ppm);

            if (layer.Config.Tiled)
            {
                var startX = MathF.Floor((-originPx.X) / sizePxLayer.X) * sizePxLayer.X + originPx.X;
                var startY = MathF.Floor((-originPx.Y) / sizePxLayer.Y) * sizePxLayer.Y + originPx.Y;
                for (var x = startX; x < sizePx.X; x += sizePxLayer.X)
                {
                    for (var y = startY; y < sizePx.Y; y += sizePxLayer.Y)
                    {
                        handle.DrawTextureRect(texture, UIBox2.FromDimensions(new Vector2(x, y), sizePxLayer), Color.White);
                    }
                }
            }
            else
            {
                handle.DrawTextureRect(texture, UIBox2.FromDimensions(originPx, sizePxLayer), Color.White);
            }
        }
    }

    private static int GetLogicalSurfaceColumnCount(FpvCameraState camera, int width)
    {
        if (width <= 1)
            return 1;

        if (width < 48)
            return Math.Max(1, width);

        if (camera.LogicalColumns > 0)
            return Math.Clamp(camera.LogicalColumns, 48, width);

        var columnStep = GetSurfaceColumnStep(camera);
        var columns = (int) MathF.Ceiling(width / (float) columnStep);
        return Math.Clamp(columns, 48, width);
    }

    private static int GetSurfaceColumnStep(FpvCameraState camera)
    {
        var configuredStep = Math.Clamp(camera.ColumnStep, 1, 8);
        return camera.QualityPreset switch
        {
            FirstPersonQualityPreset.High => 1,
            FirstPersonQualityPreset.Balanced => 1,
            _ => Math.Min(8, configuredStep * 2),
        };
    }

    private static float GetSurfaceRenderDistance(float correctedDist)
    {
        return MathF.Max(MinSurfaceRenderDistance, correctedDist);
    }

    private void DrawSurfaces(DrawingHandleScreen handle, FpvCameraState camera, int width, float height, float horizon)
    {
        _transparentSurfaces.Clear();
        var logicalColumns = GetLogicalSurfaceColumnCount(camera, width);
        var projectionPlane = _sceneBuilder.GetProjectionPlaneDistance(width, camera.FovDegrees);
        EnsureSurfaceBuffers(logicalColumns);
        var job = new SurfaceCastJob
        {
            SceneBuilder = _sceneBuilder,
            Resolver = _resolver,
            Camera = camera,
            Width = width,
            LogicalColumns = logicalColumns,
            BandLeft = _surfaceBandLeft,
            BandWidth = _surfaceBandWidth,
            RayAngle = _surfaceRayAngle,
            HasHit = _surfaceHasHit,
            HasOpaque = _surfaceHasOpaque,
            Hit = _surfaceHit,
            OpaqueHit = _surfaceOpaqueHit,
            Resolved = _surfaceResolved,
            OpaqueResolved = _surfaceOpaqueResolved,
            CorrectedDist = _surfaceCorrectedDist,
            RenderDist = _surfaceRenderDist,
            OpaqueCorrectedDist = _surfaceOpaqueCorrectedDist,
            OpaqueRenderDist = _surfaceOpaqueRenderDist,
            OccluderDist = _surfaceOccluderDist
        };
        _parallel.ProcessNow(job, logicalColumns);

        for (var column = 0; column < logicalColumns; column++)
        {
            var bandLeft = _surfaceBandLeft[column];
            var bandWidth = _surfaceBandWidth[column];
            if (!_surfaceHasHit[column])
            {
                FillDepthBuffer(width, bandLeft, bandWidth, camera.MaxDistance);
                continue;
            }

            var rayAngle = _surfaceRayAngle[column];
            var hit = _surfaceHit[column];
            var resolved = _surfaceResolved[column];
            var correctedDist = _surfaceCorrectedDist[column];
            var renderDist = _surfaceRenderDist[column];

            if (_surfaceHasOpaque[column])
            {
                var opaqueHit = _surfaceOpaqueHit[column];
                var opaqueResolved = _surfaceOpaqueResolved[column];
                var opaqueDist = _surfaceOpaqueCorrectedDist[column];
                var opaqueRenderDist = _surfaceOpaqueRenderDist[column];
                DrawSurfaceSlice(handle, camera, height, horizon, projectionPlane, bandLeft, bandWidth, rayAngle, opaqueHit, opaqueResolved, opaqueDist, opaqueRenderDist, true, true);
            }

            DrawSurfaceSlice(handle, camera, height, horizon, projectionPlane, bandLeft, bandWidth, rayAngle, hit, resolved, correctedDist, renderDist, false, false);
            FillDepthBuffer(width, bandLeft, bandWidth, _surfaceOccluderDist[column]);
        }
    }

    private void DrawSurfaceSlice(
        DrawingHandleScreen handle,
        FpvCameraState camera,
        float height,
        float horizon,
        float projectionPlane,
        int bandLeft,
        int bandWidth,
        Angle rayAngle,
        FpvRayHit hit,
        ResolvedE3DRenderable resolved,
        float correctedDist,
        float renderDist,
        bool forceOpaque,
        bool deferToAlpha)
    {
        var slice = CalculateWallSlice(camera, projectionPlane, height, horizon, resolved, correctedDist);
        var color = _sceneBuilder.ApplyDistanceFog(Color.White, hit.Distance, camera);
        if (hit.VerticalSide)
            color = new Color(color.R * SurfaceVerticalShade, color.G * SurfaceVerticalShade, color.B * SurfaceVerticalShade, color.A);

        var transparent = !forceOpaque && resolved.Transparent;
        var wallRect = UIBox2.FromDimensions(new Vector2(bandLeft, slice.Top), new Vector2(bandWidth, slice.Height));

        _sceneBuilder.GetOrderedTileSurfaceEntities(hit, _tileSurfaceEntities);
        var stackIndex = 0;
        foreach (var surfaceUid in _tileSurfaceEntities)
        {
            if (!_resolver.TryResolve(surfaceUid, out var surfaceResolved))
                continue;

            var surfaceTransparent = !forceOpaque && surfaceResolved.Transparent;
            var surfaceDrawDepth = (int) DrawDepth.Walls;
            if (TryComp(surfaceUid, out SpriteComponent? surfaceSprite))
                surfaceDrawDepth = surfaceSprite.DrawDepth;

            var depthBias = stackIndex * 0.0008f;
            stackIndex++;

            var face = GetSurfaceFaceForEntity(surfaceUid, hit);
            _resolver.GetVisibleLayers(surfaceUid, face, _surfaceLayers, false);
            if (_surfaceLayers.Count > 0)
            {
                var combinedBounds = _surfaceLayers[0].Bounds;
                for (var i = 1; i < _surfaceLayers.Count; i++)
                    combinedBounds = combinedBounds.Union(_surfaceLayers[i].Bounds);

                var useLayerBounds = surfaceResolved.SpriteMode is E3DSpriteMode.Billboard or E3DSpriteMode.Directional;
                foreach (var layer in _surfaceLayers)
                {
                    var layerColor = new Color(color.RGBA * layer.Color.RGBA).WithAlpha(surfaceTransparent ? TransparentSurfaceAlpha : 1f);
                    var layerRect = useLayerBounds ? GetLayerScreenRect(wallRect, combinedBounds, layer.Bounds) : wallRect;
                    var region = GetSurfaceTextureRegionFull(layer.Texture, hit);
                    var depthKey = correctedDist + depthBias;
                    if (surfaceTransparent || deferToAlpha)
                        _transparentSurfaces.Add(new TransparentSurfaceDraw(layer.Texture, layerRect, region, layerColor, depthKey, surfaceDrawDepth));
                    else
                        handle.DrawTextureRectRegion(layer.Texture, layerRect, region, layerColor);
                }
            }
            else
            {
                var fallback = color.WithAlpha(surfaceTransparent ? TransparentSurfaceAlpha : 1f);
                if (surfaceTransparent || deferToAlpha)
                    _transparentSurfaces.Add(new TransparentSurfaceDraw(null, wallRect, null, fallback, correctedDist + depthBias, surfaceDrawDepth));
                else
                    handle.DrawRect(wallRect, fallback);
            }
        }
    }

    private UIBox2 GetSurfaceTextureRegionFull(Texture texture, FpvRayHit hit)
    {
        var frac = GetHitTextureFraction(hit);
        var texX = Math.Clamp(MathF.Floor(frac * (float) texture.Width), 0f, (float) texture.Width - 1f);
        return new UIBox2(texX, 0f, texX + 1f, texture.Height);
    }

    private float GetHitTextureFraction(FpvRayHit hit)
    {
        var local = hit.GridLocalHitPos;
        var frac = hit.VerticalSide
            ? local.Y - MathF.Floor(local.Y)
            : local.X - MathF.Floor(local.X);

        return Math.Clamp(frac, 0f, 0.999f);
    }

    private static WallSliceProjection CalculateWallSlice(
        FpvCameraState camera,
        float projectionPlane,
        float screenHeight,
        float horizon,
        ResolvedE3DRenderable resolved,
        float correctedDist)
    {
        var perpDist = MathF.Max(MinCorrectedDistance, correctedDist);
        var wallHeightPx = projectionPlane * resolved.Height / perpDist;
        wallHeightPx = MathF.Min(screenHeight * MaxSurfaceScreenHeightMultiplier, wallHeightPx);

        var bottom = horizon + projectionPlane * (camera.EyeHeight - resolved.EyeOffset) / perpDist;
        var top = bottom - wallHeightPx;
        return new WallSliceProjection(top, wallHeightPx, perpDist);
    }

    private static UIBox2 GetLayerScreenRect(UIBox2 combinedRect, Box2 combinedBounds, Box2 layerBounds)
    {
        var width = MathF.Max(MinNonZero, combinedBounds.Width);
        var height = MathF.Max(MinNonZero, combinedBounds.Height);

        var left = combinedRect.Left + ((layerBounds.Left - combinedBounds.Left) / width) * combinedRect.Width;
        var right = combinedRect.Left + ((layerBounds.Right - combinedBounds.Left) / width) * combinedRect.Width;
        var top = combinedRect.Top + ((combinedBounds.Top - layerBounds.Top) / height) * combinedRect.Height;
        var bottom = combinedRect.Top + ((combinedBounds.Top - layerBounds.Bottom) / height) * combinedRect.Height;

        return new UIBox2(left, top, right, bottom);
    }

    private static bool TryClipRectHorizontally(UIBox2 rect, float visibleLeft, float visibleRight, out UIBox2 clippedRect, out float clipStart, out float clipEnd)
    {
        clippedRect = default;
        clipStart = 0f;
        clipEnd = 1f;

        var width = rect.Width;
        if (width <= MinNonZero)
            return false;

        var left = Math.Clamp(visibleLeft, rect.Left, rect.Right);
        var right = Math.Clamp(visibleRight, rect.Left, rect.Right);
        if (right - left <= MinNonZero)
            return false;

        clipStart = (left - rect.Left) / width;
        clipEnd = (right - rect.Left) / width;
        clippedRect = new UIBox2(left, rect.Top, right, rect.Bottom);
        return true;
    }

    private static UIBox2 GetHorizontalTextureRegion(Texture texture, float clipStart, float clipEnd)
    {
        var textureWidth = Math.Max(1f, texture.Width);
        var left = Math.Clamp(clipStart * textureWidth, 0f, textureWidth - 1f);
        var right = Math.Clamp(clipEnd * textureWidth, left + 1f, textureWidth);
        return new UIBox2(left, 0f, right, texture.Height);
    }

    private void DrawAlphaPass(DrawingHandleScreen handle, FpvCameraState camera, int width, int height)
    {
        _sceneBuilder.CollectBillboards(camera, _depthBuffer, width, height, _billboards);
        _alphaDraws.Clear();
        var requiredAlphaCapacity = _transparentSurfaces.Count + _billboards.Count;
        if (_alphaDraws.Capacity < requiredAlphaCapacity)
            _alphaDraws.Capacity = requiredAlphaCapacity;

        for (var i = 0; i < _transparentSurfaces.Count; i++)
        {
            var surface = _transparentSurfaces[i];
            _alphaDraws.Add(new AlphaDraw(surface.Depth, surface.DrawDepth, AlphaDrawKind.Surface, i));
        }

        for (var i = 0; i < _billboards.Count; i++)
        {
            var billboard = _billboards[i];
            _alphaDraws.Add(new AlphaDraw(billboard.Distance, billboard.DrawDepth, AlphaDrawKind.Billboard, i));
        }

        _alphaDraws.Sort(static (a, b) =>
        {
            var depthCmp = b.Depth.CompareTo(a.Depth);
            if (depthCmp != 0)
                return depthCmp;

            var drawCmp = a.DrawDepth.CompareTo(b.DrawDepth);
            if (drawCmp != 0)
                return drawCmp;

            return a.Kind.CompareTo(b.Kind);
        });

        foreach (var draw in _alphaDraws)
        {
            if (draw.Kind == AlphaDrawKind.Surface)
            {
                var surface = _transparentSurfaces[draw.Index];
                if (surface.Texture != null && surface.Region.HasValue)
                    handle.DrawTextureRectRegion(surface.Texture, surface.Rect, surface.Region.Value, surface.Color);
                else
                    handle.DrawRect(surface.Rect, surface.Color);
                continue;
            }

            var billboard = _billboards[draw.Index];
            _resolver.GetVisibleLayers(billboard.Entity, billboard.Face, _billboardLayers, true);
            if (_billboardLayers.Count > 0 && billboard.CombinedBounds.Size.LengthSquared() > MinLayerBoundsSizeSquared)
            {
                foreach (var layer in _billboardLayers)
                {
                    var layerRect = GetLayerScreenRect(billboard.ScreenRect, billboard.CombinedBounds, layer.Bounds);
                    if (!TryClipRectHorizontally(layerRect, billboard.VisibleLeft, billboard.VisibleRight, out var clippedLayerRect, out var clipStart, out var clipEnd))
                        continue;

                    var layerColor = _sceneBuilder.ApplyDistanceFog(layer.Color, billboard.Distance, camera)
                        .WithAlpha(billboard.Transparent ? TransparentBillboardAlpha : 1f);
                    handle.DrawTextureRectRegion(layer.Texture, clippedLayerRect, GetHorizontalTextureRegion(layer.Texture, clipStart, clipEnd), layerColor);
                }
            }
            else if (billboard.Texture != null)
            {
                if (!TryClipRectHorizontally(billboard.ScreenRect, billboard.VisibleLeft, billboard.VisibleRight, out var clippedRect, out var clipStart, out var clipEnd))
                    continue;

                handle.DrawTextureRectRegion(
                    billboard.Texture,
                    clippedRect,
                    GetHorizontalTextureRegion(billboard.Texture, clipStart, clipEnd),
                    billboard.Color.WithAlpha(billboard.Transparent ? TransparentBillboardAlpha : 1f));
            }
        }
    }

    private void BuildFloorDecalBins()
    {
        _floorDecalBins.Clear();
        for (var i = 0; i < _floorDecals.Count; i++)
        {
            var decal = _floorDecals[i];
            var halfWidth = decal.WorldWidth * 0.5f;
            var halfDepth = decal.WorldDepth * 0.5f;
            var extent = MathF.Max(halfWidth, halfDepth);
            var min = decal.WorldPosition - new Vector2(extent, extent);
            var max = decal.WorldPosition + new Vector2(extent, extent);
            var minBinX = (int) MathF.Floor(min.X / FloorDecalBinSize);
            var maxBinX = (int) MathF.Floor(max.X / FloorDecalBinSize);
            var minBinY = (int) MathF.Floor(min.Y / FloorDecalBinSize);
            var maxBinY = (int) MathF.Floor(max.Y / FloorDecalBinSize);

            for (var x = minBinX; x <= maxBinX; x++)
            {
                for (var y = minBinY; y <= maxBinY; y++)
                {
                    var key = new Vector2i(x, y);
                    if (!_floorDecalBins.TryGetValue(key, out var list))
                    {
                        list = new List<int>(2);
                        _floorDecalBins[key] = list;
                    }

                    list.Add(i);
                }
            }
        }
    }

    private bool TryGetFloorDecalSample(Vector2 world, out Texture? texture, out UIBox2? region, out Color color)
    {
        texture = null;
        region = null;
        color = Color.White;

        var binX = (int) MathF.Floor(world.X / FloorDecalBinSize);
        var binY = (int) MathF.Floor(world.Y / FloorDecalBinSize);
        if (!_floorDecalBins.TryGetValue(new Vector2i(binX, binY), out var indices))
            return false;

        foreach (var index in indices)
        {
            var decal = _floorDecals[index];
            var local = world - decal.WorldPosition;
            var cos = MathF.Cos((float) -decal.Rotation.Theta);
            var sin = MathF.Sin((float) -decal.Rotation.Theta);
            var rotated = new Vector2(
                local.X * cos - local.Y * sin,
                local.X * sin + local.Y * cos);

            var halfWidth = decal.WorldWidth * 0.5f;
            var halfDepth = decal.WorldDepth * 0.5f;
            if (MathF.Abs(rotated.X) > halfWidth || MathF.Abs(rotated.Y) > halfDepth)
                continue;

            var u = Math.Clamp((rotated.X + halfWidth) / MathF.Max(MinNonZero, decal.WorldWidth), 0f, 1f);
            var v = Math.Clamp(1f - (rotated.Y + halfDepth) / MathF.Max(MinNonZero, decal.WorldDepth), 0f, 1f);
            var texX = Math.Clamp(MathF.Floor(u * (float) decal.Texture.Width), 0f, (float) decal.Texture.Width - 1f);
            var texY = Math.Clamp(MathF.Floor(v * (float) decal.Texture.Height), 0f, (float) decal.Texture.Height - 1f);
            texture = decal.Texture;
            region = new UIBox2(texX, texY, texX + 1f, texY + 1f);
            color = decal.Color;
            return true;
        }

        return false;
    }

    private Direction GetSurfaceFaceForEntity(EntityUid uid, FpvRayHit hit)
    {
        if (hit.WorldNormal.LengthSquared() <= MinRelativeLengthSquared)
            return hit.WallFace;

        var worldRot = _sceneBuilder.TransformSystem.GetWorldRotation(uid);
        var localNormal = (-worldRot).RotateVec(hit.WorldNormal);
        if (localNormal.LengthSquared() <= MinRelativeLengthSquared)
            return hit.WallFace;

        return Angle.FromWorldVec(localNormal).GetCardinalDir();
    }

    private void PublishInteractionHit(FpvCameraState camera)
    {
        if (_sceneBuilder.TryCastInteractionRay(camera, out var hit))
            _interaction.SetInteractionHit(hit);
        else
            _interaction.Clear();
    }

    private static void DrawCrosshairGlyph(DrawingHandleScreen handle, float cx, float cy)
    {
        var crossColor = Color.White.WithAlpha(CrosshairAlpha);
        handle.DrawRect(
            UIBox2.FromDimensions(
                new Vector2(cx - CrosshairHalfLengthPx, cy - CrosshairHalfThicknessPx),
                new Vector2(CrosshairHalfLengthPx * 2, CrosshairHalfThicknessPx * 2)),
            crossColor);
        handle.DrawRect(
            UIBox2.FromDimensions(
                new Vector2(cx - CrosshairHalfThicknessPx, cy - CrosshairHalfLengthPx),
                new Vector2(CrosshairHalfThicknessPx * 2, CrosshairHalfLengthPx * 2)),
            crossColor);
    }

    private void EnsureDepthBuffer(int width)
    {
        if (_depthBuffer.Length == width)
            return;

        _depthBuffer = new float[width];
    }

    private void EnsureSurfaceBuffers(int logicalColumns)
    {
        if (_surfaceBandLeft.Length == logicalColumns)
            return;

        _surfaceBandLeft = new int[logicalColumns];
        _surfaceBandWidth = new int[logicalColumns];
        _surfaceRayAngle = new Angle[logicalColumns];
        _surfaceHasHit = new bool[logicalColumns];
        _surfaceHasOpaque = new bool[logicalColumns];
        _surfaceHit = new FpvRayHit[logicalColumns];
        _surfaceOpaqueHit = new FpvRayHit[logicalColumns];
        _surfaceResolved = new ResolvedE3DRenderable[logicalColumns];
        _surfaceOpaqueResolved = new ResolvedE3DRenderable[logicalColumns];
        _surfaceCorrectedDist = new float[logicalColumns];
        _surfaceRenderDist = new float[logicalColumns];
        _surfaceOpaqueCorrectedDist = new float[logicalColumns];
        _surfaceOpaqueRenderDist = new float[logicalColumns];
        _surfaceOccluderDist = new float[logicalColumns];
    }

    private void FillDepthBuffer(int width, int start, int step, float value)
    {
        var end = Math.Min(width, start + step);
        for (var i = start; i < end; i++)
            _depthBuffer[i] = value;
    }

    private readonly record struct SurfaceCastJob : IParallelRobustJob
    {
        public int BatchSize => 8;

        public required FirstPersonSceneBuilderSystem SceneBuilder { get; init; }
        public required E3DArchetypeResolverSystem Resolver { get; init; }
        public required FpvCameraState Camera { get; init; }
        public required int Width { get; init; }
        public required int LogicalColumns { get; init; }

        public required int[] BandLeft { get; init; }
        public required int[] BandWidth { get; init; }
        public required Angle[] RayAngle { get; init; }
        public required bool[] HasHit { get; init; }
        public required bool[] HasOpaque { get; init; }
        public required FpvRayHit[] Hit { get; init; }
        public required FpvRayHit[] OpaqueHit { get; init; }
        public required ResolvedE3DRenderable[] Resolved { get; init; }
        public required ResolvedE3DRenderable[] OpaqueResolved { get; init; }
        public required float[] CorrectedDist { get; init; }
        public required float[] RenderDist { get; init; }
        public required float[] OpaqueCorrectedDist { get; init; }
        public required float[] OpaqueRenderDist { get; init; }
        public required float[] OccluderDist { get; init; }

        public void Execute(int index)
        {
            var bandLeft = (int) MathF.Floor(index * Width / (float) LogicalColumns);
            var bandRight = Math.Max(bandLeft + 1, (int) MathF.Ceiling((index + 1) * Width / (float) LogicalColumns));
            var bandWidth = Math.Max(1, bandRight - bandLeft);
            var sampleX = bandLeft + bandWidth * 0.5f;

            BandLeft[index] = bandLeft;
            BandWidth[index] = bandWidth;

            HasOpaque[index] = false;

            if (!SceneBuilder.TryCastSurfaceRay(Camera, sampleX, Width, out var hit) ||
                !Resolver.TryResolve(hit.HitEntity, out var resolved))
            {
                HasHit[index] = false;
                OccluderDist[index] = Camera.MaxDistance;
                return;
            }

            var rayAngle = SceneBuilder.GetRayAngle(Camera, sampleX, Width);
            RayAngle[index] = rayAngle;
            Hit[index] = hit;
            Resolved[index] = resolved;
            HasHit[index] = true;

            var correctedDist = MathF.Max(MinCorrectedDistance, hit.Distance * (float) Math.Cos((rayAngle - Camera.Yaw).Theta));
            CorrectedDist[index] = correctedDist;
            RenderDist[index] = GetSurfaceRenderDistance(correctedDist);

            if (!resolved.Transparent)
            {
                OccluderDist[index] = correctedDist;
                return;
            }

            if (SceneBuilder.TryCastOpaqueSurfaceRay(Camera, sampleX, Width, out var opaqueHit) &&
                Resolver.TryResolve(opaqueHit.HitEntity, out var opaqueResolved))
            {
                HasOpaque[index] = true;
                OpaqueHit[index] = opaqueHit;
                OpaqueResolved[index] = opaqueResolved;
                var opaqueDist = MathF.Max(MinCorrectedDistance, opaqueHit.Distance * (float) Math.Cos((rayAngle - Camera.Yaw).Theta));
                OpaqueCorrectedDist[index] = opaqueDist;
                OpaqueRenderDist[index] = GetSurfaceRenderDistance(opaqueDist);
                OccluderDist[index] = opaqueDist;
            }
            else
            {
                OccluderDist[index] = Camera.MaxDistance;
            }
        }
    }

    private readonly record struct FloorDrawPoint(
        Texture Texture,
        UIBox2 Region,
        Color Color,
        bool HasDecal,
        Texture? DecalTexture,
        UIBox2? DecalRegion,
        Color DecalColor);

    private record struct FloorSpanState(int X, int Width, FloorDrawPoint Point)
    {
        public bool CanExtend(FloorDrawPoint point)
        {
            if (!ReferenceEquals(Point.Texture, point.Texture))
                return false;

            if (!Point.Region.Equals(point.Region) || !Point.Color.Equals(point.Color))
                return false;

            if (Point.HasDecal != point.HasDecal)
                return false;

            if (!Point.HasDecal)
                return true;

            if (!ReferenceEquals(Point.DecalTexture, point.DecalTexture))
                return false;

            return Point.DecalRegion.Equals(point.DecalRegion) && Point.DecalColor.Equals(point.DecalColor);
        }
    }

    private readonly record struct TransparentSurfaceDraw(Texture? Texture, UIBox2 Rect, UIBox2? Region, Color Color, float Depth, int DrawDepth);

    private readonly record struct AlphaDraw(float Depth, int DrawDepth, AlphaDrawKind Kind, int Index);
    private readonly record struct WallSliceProjection(float Top, float Height, float PerpDist);

    private enum AlphaDrawKind
    {
        Surface = 0,
        Billboard = 1
    }
}
