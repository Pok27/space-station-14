using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Parallax;
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
    private const float MinSurfaceRenderDistance = 0.08f;
    private const float MinCorrectedDistance = 0.05f;
    private const float NearSurfaceSofteningDistance = 0.9f;
    private const float NearSurfaceSofteningStrength = 0.08f;
    private const float MaxSurfaceScreenHeightMultiplier = 4f;
    private const float MinLayerBoundsSizeSquared = 0.0001f;
    private const float MinRelativeLengthSquared = 0.0001f;
    private const float MinNonZero = 0.0001f;
    private const float HorizonEpsilon = 0.01f;
    private const float OcclusionEpsilon = 0.01f;
    private const float SurfaceVerticalShade = 0.92f;
    private const float SurfaceFloorOverlapPixels = 1.25f;
    private const float TransparentSurfaceAlpha = 0.6f;
    private const float TransparentBillboardAlpha = 0.75f;
    private const float CrosshairAlpha = 0.7f;
    private const int CrosshairHalfLengthPx = 6;
    private const int CrosshairHalfThicknessPx = 1;
    private static readonly Color BackgroundTopColor = new(0, 0, 0);
    private static readonly Color BackgroundBottomColor = new(0, 0, 0);

    [Dependency] private readonly FirstPersonFloorCacheSystem _floorCache = default!;
    [Dependency] private readonly FirstPersonInteractionSystem _interaction = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ParallaxSystem _parallaxSystem = default!;
    [Dependency] private readonly FirstPersonSceneBuilderSystem _sceneBuilder = default!;
    [Dependency] private readonly E3DArchetypeResolverSystem _resolver = default!;
    [Dependency] private readonly IParallelManager _parallel = default!;

    private readonly List<FpvBillboard> _billboards = new();
    private readonly List<FpvVisualLayer> _billboardLayers = new();
    private readonly List<FpvFloorDecal> _floorDecals = new();
    private readonly List<FloorRowDescriptor> _floorRows = new();
    private readonly List<FpvVisualLayer> _surfaceLayers = new();
    private readonly List<TransparentSurfaceDraw> _transparentSurfaces = new();
    private readonly List<AlphaDraw> _alphaDraws = new();
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
    private FloorRowDescriptor[] _floorRowBuffer = Array.Empty<FloorRowDescriptor>();
    private bool[] _floorSampleValid = Array.Empty<bool>();
    private Texture?[] _floorSampleTexture = Array.Empty<Texture?>();
    private UIBox2[] _floorSampleRegion = Array.Empty<UIBox2>();
    private Color[] _floorSampleColor = Array.Empty<Color>();
    private Vector2[] _floorSampleWorld = Array.Empty<Vector2>();

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

        DrawBackground(handle, sizePx, horizon);
        DrawSurfaces(handle, camera, width, height, horizon);
        _sceneBuilder.CollectFloorDecals(camera, _depthBuffer, width, _floorDecals);
        DrawFloor(handle, camera, width, height, horizon);
        DrawAlphaPass(handle, camera, width, (int) height);
        PublishInteractionHit(camera, width, (int) height);

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

    private void DrawBackground(DrawingHandleScreen handle, Vector2 sizePx, float horizon)
    {
        var topHeight = Math.Clamp(horizon, 0f, sizePx.Y);
        handle.DrawRect(UIBox2.FromDimensions(Vector2.Zero, new Vector2(sizePx.X, topHeight)), BackgroundTopColor);
        handle.DrawRect(
            UIBox2.FromDimensions(new Vector2(0f, topHeight), new Vector2(sizePx.X, Math.Max(0f, sizePx.Y - topHeight))),
            BackgroundBottomColor);
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
        var logicalColumns = GetLogicalFloorColumnCount(camera, width);
        var invLogicalColumns = 1f / logicalColumns;
        _floorRows.Clear();

        for (var y = Math.Max(0, (int) MathF.Ceiling(horizon)); y < height;)
        {
            var p = y - horizon;
            if (p <= HorizonEpsilon)
            {
                y += 1;
                continue;
            }

            var rowDistance = camera.EyeHeight * projectionPlane / p;
            if (rowDistance > camera.MaxDistance)
            {
                y += 4;
                continue;
            }

            var yStep = rowDistance switch
            {
                > 8f => 7,
                > 6f => 6,
                > 4f => 5,
                > 2.5f => 4,
                _ => 3
            };

            var stepVec = rowDistance * (rayRight - rayLeft) * invLogicalColumns;
            var world = camera.EyePos + rayLeft * rowDistance;
            _floorRows.Add(new FloorRowDescriptor(y, yStep, rowDistance, world, stepVec));
            y += yStep;
        }

        if (_floorRows.Count == 0)
            return;

        EnsureFloorPrepassBuffers(_floorRows.Count, logicalColumns);
        _floorRows.CopyTo(_floorRowBuffer, 0);
        var sampleCount = _floorRows.Count * logicalColumns;
        var parallaxLayers = _parallaxSystem.GetParallaxLayers(camera.MapId);
        var realTime = _timing.RealTime;
        var job = new FloorPrepassJob
        {
            Camera = camera,
            SceneBuilder = _sceneBuilder,
            FloorCache = _floorCache,
            ParallaxLayers = parallaxLayers,
            RealTime = _timing.RealTime,
            DepthBuffer = _depthBuffer,
            Width = width,
            LogicalColumns = logicalColumns,
            Rows = _floorRowBuffer,
            SampleValid = _floorSampleValid,
            SampleTexture = _floorSampleTexture,
            SampleRegion = _floorSampleRegion,
            SampleColor = _floorSampleColor,
            SampleWorld = _floorSampleWorld
        };
        _parallel.ProcessNow(job, sampleCount);

        for (var rowIndex = 0; rowIndex < _floorRows.Count; rowIndex++)
        {
            var row = _floorRows[rowIndex];
            for (var column = 0; column < logicalColumns; column++)
            {
                var bandLeft = (int) MathF.Floor(column * width / (float) logicalColumns);
                var bandRight = Math.Max(bandLeft + 1, (int) MathF.Ceiling((column + 1) * width / (float) logicalColumns));
                var bandWidth = Math.Max(1, bandRight - bandLeft);
                var sampleIndex = rowIndex * logicalColumns + column;
                var dest = UIBox2.FromDimensions(new Vector2(bandLeft, row.ScreenY), new Vector2(bandWidth, row.ScreenHeight));
                var world = row.WorldStart + row.StepVec * column;
                var depthStart = Math.Clamp(bandLeft, 0, Math.Max(0, _depthBuffer.Length - 1));
                var depthEnd = Math.Clamp(bandRight - 1, 0, Math.Max(0, _depthBuffer.Length - 1));
                var minDepth = float.PositiveInfinity;
                for (var i = depthStart; i <= depthEnd; i++)
                    minDepth = MathF.Min(minDepth, _depthBuffer[i]);

                if (minDepth > 0f && row.Distance >= minDepth - OcclusionEpsilon)
                    continue;

                if (TryGetParallaxFloorSample(parallaxLayers, camera, world, realTime, out var parallaxSample))
                {
                    handle.DrawTextureRectRegion(
                        parallaxSample.Texture,
                        dest,
                        GetFloorSampleRegion(parallaxSample, row.Distance),
                        _sceneBuilder.ApplyDistanceFog(Color.White, row.Distance, camera));
                }

                if (_floorSampleValid[sampleIndex] && _floorSampleTexture[sampleIndex] != null)
                    handle.DrawTextureRectRegion(_floorSampleTexture[sampleIndex]!, dest, _floorSampleRegion[sampleIndex], _floorSampleColor[sampleIndex]);

                if (TryGetFloorDecalSample(world, out var decalTexture, out var decalRegion, out var decalColor))
                    handle.DrawTextureRectRegion(decalTexture, dest, decalRegion, decalColor);
            }
        }
    }

    private static UIBox2 GetFloorSampleRegion(FpvFloorSample floorSample, float distance)
    {
        var variantRegion = floorSample.TextureRegion;
        var tilePixels = variantRegion.Width;
        var texX = variantRegion.Left + floorSample.FracX * tilePixels;
        var texY = variantRegion.Top + (1f - floorSample.FracY) * variantRegion.Height;
        var sampleSize = distance switch
        {
            > 8f => 2f,
            > 4f => 1f,
            _ => 1f
        };

        texX = MathF.Floor(texX / sampleSize) * sampleSize;
        texY = MathF.Floor(texY / sampleSize) * sampleSize;
        texX = Math.Clamp(texX, variantRegion.Left, variantRegion.Right - sampleSize);
        texY = Math.Clamp(texY, variantRegion.Top, variantRegion.Bottom - sampleSize);
        return new UIBox2(texX, texY, texX + sampleSize, texY + sampleSize);
    }

    private static bool TryGetParallaxFloorSample(ParallaxLayerPrepared[] parallaxLayers, FpvCameraState camera, Vector2 world, TimeSpan realTime, out FpvFloorSample sample)
    {
        sample = default;

        for (var i = 0; i < parallaxLayers.Length; i++)
        {
            var layer = parallaxLayers[i];
            if (!layer.Config.Tiled)
                continue;

            var texture = layer.Texture;
            var scale = layer.Config.Scale;
            if (texture.Width <= 0 || texture.Height <= 0 || scale.X <= 0f || scale.Y <= 0f)
                continue;

            var sizeWorld = new Vector2(
                texture.Width / (float) EyeManager.PixelsPerMeter * scale.X,
                texture.Height / (float) EyeManager.PixelsPerMeter * scale.Y);

            if (sizeWorld.X <= MinNonZero || sizeWorld.Y <= MinNonZero)
                continue;

            var home = layer.Config.WorldHomePosition;
            var scrolled = layer.Config.Scrolling * (float) realTime.TotalSeconds;
            var origin = (camera.EyePos - home) * layer.Config.Slowness;
            origin += home;
            origin += layer.Config.WorldAdjustPosition;
            origin += scrolled;
            origin -= sizeWorld / 2f;

            var local = world - origin;
            var fracX = local.X / sizeWorld.X;
            var fracY = local.Y / sizeWorld.Y;
            fracX -= MathF.Floor(fracX);
            fracY -= MathF.Floor(fracY);

            sample = new FpvFloorSample(
                texture,
                new UIBox2(0f, 0f, texture.Width, texture.Height),
                fracX,
                fracY);
            return true;
        }

        return false;
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
            FirstPersonQualityPreset.High => Math.Max(1, configuredStep / 2),
            FirstPersonQualityPreset.Balanced => configuredStep,
            _ => Math.Min(8, configuredStep * 2),
        };
    }

    private static int GetLogicalFloorColumnCount(FpvCameraState camera, int width)
    {
        if (width <= 1)
            return 1;

        var columnStep = camera.QualityPreset switch
        {
            FirstPersonQualityPreset.High => Math.Max(1, camera.ColumnStep),
            FirstPersonQualityPreset.Balanced => Math.Max(2, camera.ColumnStep * 2),
            _ => Math.Max(3, camera.ColumnStep * 3),
        };

        var columns = (int) MathF.Ceiling(width / (float) columnStep);
        return Math.Clamp(columns, 48, width);
    }

    private static float GetSurfaceRenderDistance(float correctedDist)
    {
        var clamped = MathF.Max(MinCorrectedDistance, correctedDist);
        if (clamped >= NearSurfaceSofteningDistance)
            return MathF.Max(MinSurfaceRenderDistance, clamped);

        var t = 1f - clamped / NearSurfaceSofteningDistance;
        var softened = clamped + t * t * NearSurfaceSofteningStrength;
        return MathF.Max(MinSurfaceRenderDistance, softened);
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
                FillDepthBuffer(width, bandLeft, bandWidth, 0f);
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
        if (hit.FogOccluded)
        {
            return;
        }

        // Geometry must be projected from true corrected distance, otherwise
        // near-wall softening pulls wall bottoms upward and "cuts" them.
        var projectionDist = MathF.Max(MinCorrectedDistance, correctedDist);
        var lineHeight = MathF.Min(height * MaxSurfaceScreenHeightMultiplier, projectionPlane * resolved.Height / projectionDist) + SurfaceFloorOverlapPixels;
        // Anchor world surfaces to the floor plane instead of centering around horizon.
        // This prevents "half-cut" walls when eye height is not exactly half wall height.
        var groundY = horizon + projectionPlane * camera.EyeHeight / projectionDist;
        var top = groundY - lineHeight - projectionPlane * resolved.EyeOffset / projectionDist;
        var color = _sceneBuilder.ApplyDistanceFog(Color.White, hit.Distance, camera);
        if (hit.VerticalSide)
            color = new Color(color.R * SurfaceVerticalShade, color.G * SurfaceVerticalShade, color.B * SurfaceVerticalShade, color.A);

        var transparent = !forceOpaque && resolved.Transparent;
        var wallRect = UIBox2.FromDimensions(new Vector2(bandLeft, top), new Vector2(bandWidth, lineHeight));
        var surfaceDrawDepth = (int) DrawDepth.Walls;
        if (TryComp(hit.HitEntity, out SpriteComponent? sprite))
            surfaceDrawDepth = sprite.DrawDepth;

        _resolver.GetVisibleLayers(hit.HitEntity, Direction.Invalid, _surfaceLayers, false);
        if (_surfaceLayers.Count > 0)
        {
            var combinedBounds = _surfaceLayers[0].Bounds;
            for (var i = 1; i < _surfaceLayers.Count; i++)
                combinedBounds = combinedBounds.Union(_surfaceLayers[i].Bounds);

            var useLayerBounds = resolved.SpriteMode == E3DSpriteMode.Billboard;
            foreach (var layer in _surfaceLayers)
            {
                var layerColor = new Color(color.RGBA * layer.Color.RGBA).WithAlpha(transparent ? TransparentSurfaceAlpha : 1f);
                var layerRect = useLayerBounds ? GetLayerScreenRect(wallRect, combinedBounds, layer.Bounds) : wallRect;
                var region = GetSurfaceTextureRegion(layer.Texture, hit);
                if (transparent || deferToAlpha)
                    _transparentSurfaces.Add(new TransparentSurfaceDraw(layer.Texture, layerRect, region, layerColor, correctedDist, surfaceDrawDepth));
                else
                    handle.DrawTextureRectRegion(layer.Texture, layerRect, region, layerColor);
            }
        }
        else
        {
            var fallback = color.WithAlpha(transparent ? TransparentSurfaceAlpha : 1f);
            if (transparent || deferToAlpha)
                _transparentSurfaces.Add(new TransparentSurfaceDraw(null, wallRect, null, fallback, correctedDist, surfaceDrawDepth));
            else
                handle.DrawRect(wallRect, fallback);
        }
    }

    private static UIBox2 GetSurfaceTextureRegion(Texture texture, FpvRayHit hit)
    {
        var frac = GetHitTextureFraction(hit);
        var texX = Math.Clamp(MathF.Floor(frac * (float) texture.Width), 0f, (float) texture.Width - 1f);
        return new UIBox2(texX, 0f, texX + 1f, texture.Height);
    }

    private static float GetHitTextureFraction(FpvRayHit hit)
    {
        var frac = hit.VerticalSide
            ? hit.HitPos.Y - MathF.Floor(hit.HitPos.Y)
            : hit.HitPos.X - MathF.Floor(hit.HitPos.X);

        return Math.Clamp(frac, 0f, 0.999f);
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

    private bool TryGetFloorDecalSample(Vector2 world, out Texture texture, out UIBox2 region, out Color color)
    {
        texture = default!;
        region = default;
        color = Color.White;

        foreach (var decal in _floorDecals)
        {
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
            region = new UIBox2(texX, texY, texX + 1, texY + 1);
            color = decal.Color;
            return true;
        }

        return false;
    }

    private void PublishInteractionHit(FpvCameraState camera, int width, int height)
    {
        if (TryGetProjectedInteractionHit(camera, width, height, out var hit) ||
            _sceneBuilder.TryCastInteractionRay(camera, out hit))
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

    private void EnsureFloorPrepassBuffers(int rowCount, int logicalColumns)
    {
        if (_floorRowBuffer.Length < rowCount)
            _floorRowBuffer = new FloorRowDescriptor[rowCount];

        var sampleCount = rowCount * logicalColumns;
        if (_floorSampleValid.Length >= sampleCount)
            return;

        _floorSampleValid = new bool[sampleCount];
        _floorSampleTexture = new Texture?[sampleCount];
        _floorSampleRegion = new UIBox2[sampleCount];
        _floorSampleColor = new Color[sampleCount];
        _floorSampleWorld = new Vector2[sampleCount];
    }

    private void FillDepthBuffer(int width, int start, int step, float value)
    {
        var end = Math.Min(width, start + step);
        for (var i = start; i < end; i++)
            _depthBuffer[i] = value;
    }

    private bool TryGetProjectedInteractionHit(FpvCameraState camera, int width, int height, out FpvInteractionHit hit)
    {
        hit = default;
        if (_billboards.Count == 0)
            return false;

        var centerX = width * 0.5f;
        var centerY = height * 0.5f;
        FpvProjectedInteractionCandidate? bestCandidate = null;

        foreach (var billboard in _billboards)
        {
            if (centerX < billboard.VisibleLeft || centerX > billboard.VisibleRight)
                continue;

            if (centerY < billboard.ScreenRect.Top || centerY > billboard.ScreenRect.Bottom)
                continue;

            if (!_sceneBuilder.TryGetInteractionPoint(billboard.Entity, camera.MapId, out var coords, out var distance) ||
                distance > camera.InteractionDistance)
            {
                continue;
            }

            if (!_sceneBuilder.TryGetInteractionDrawOrder(billboard.Entity, out var drawDepth, out var renderOrder))
                continue;

            var candidate = new FpvProjectedInteractionCandidate(billboard.Entity, coords, distance, drawDepth, renderOrder);
            if (bestCandidate == null || CompareInteractionCandidates(candidate, bestCandidate.Value) < 0)
                bestCandidate = candidate;
        }

        if (bestCandidate == null)
            return false;

        var chosen = bestCandidate.Value;
        hit = new FpvInteractionHit(chosen.Entity, chosen.Coordinates, chosen.Distance);
        return true;
    }

    private static int CompareInteractionCandidates(FpvProjectedInteractionCandidate x, FpvProjectedInteractionCandidate y)
    {
        var distanceCmp = x.Distance.CompareTo(y.Distance);
        if (distanceCmp != 0)
            return distanceCmp;

        var drawDepthCmp = y.DrawDepth.CompareTo(x.DrawDepth);
        if (drawDepthCmp != 0)
            return drawDepthCmp;

        var renderOrderCmp = y.RenderOrder.CompareTo(x.RenderOrder);
        if (renderOrderCmp != 0)
            return renderOrderCmp;

        return x.Entity.CompareTo(y.Entity);
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

            if (!SceneBuilder.TryCastSurfaceRay(Camera, sampleX, Width, out var hit))
            {
                HasHit[index] = false;
                OccluderDist[index] = 0f;
                return;
            }

            var rayAngle = SceneBuilder.GetRayAngle(Camera, sampleX, Width);
            RayAngle[index] = rayAngle;
            Hit[index] = hit;
            HasHit[index] = true;

            var correctedDist = MathF.Max(MinCorrectedDistance, hit.Distance * (float) Math.Cos((rayAngle - Camera.Yaw).Theta));
            CorrectedDist[index] = correctedDist;
            RenderDist[index] = GetSurfaceRenderDistance(correctedDist);

            if (hit.FogOccluded)
            {
                OccluderDist[index] = correctedDist;
                return;
            }

            if (!Resolver.TryResolve(hit.HitEntity, out var resolved))
            {
                HasHit[index] = false;
                OccluderDist[index] = 0f;
                return;
            }

            Resolved[index] = resolved;

            if (!resolved.Transparent)
            {
                OccluderDist[index] = correctedDist;
                return;
            }

            if (SceneBuilder.TryCastOpaqueSurfaceRay(Camera, sampleX, Width, out var opaqueHit))
            {
                var opaqueDist = MathF.Max(MinCorrectedDistance, opaqueHit.Distance * (float) Math.Cos((rayAngle - Camera.Yaw).Theta));
                if (opaqueHit.FogOccluded)
                {
                    OccluderDist[index] = opaqueDist;
                    return;
                }

                if (Resolver.TryResolve(opaqueHit.HitEntity, out var opaqueResolved))
                {
                    HasOpaque[index] = true;
                    OpaqueHit[index] = opaqueHit;
                    OpaqueResolved[index] = opaqueResolved;
                    OpaqueCorrectedDist[index] = opaqueDist;
                    OpaqueRenderDist[index] = GetSurfaceRenderDistance(opaqueDist);
                    OccluderDist[index] = opaqueDist;
                    return;
                }
            }

            OccluderDist[index] = Camera.MaxDistance;
        }
    }

    private readonly record struct FloorPrepassJob : IParallelRobustJob
    {
        public int BatchSize => 64;

        public required FpvCameraState Camera { get; init; }
        public required FirstPersonSceneBuilderSystem SceneBuilder { get; init; }
        public required FirstPersonFloorCacheSystem FloorCache { get; init; }
        public required ParallaxLayerPrepared[] ParallaxLayers { get; init; }
        public required TimeSpan RealTime { get; init; }
        public required float[] DepthBuffer { get; init; }
        public required int Width { get; init; }
        public required int LogicalColumns { get; init; }
        public required FloorRowDescriptor[] Rows { get; init; }
        public required bool[] SampleValid { get; init; }
        public required Texture?[] SampleTexture { get; init; }
        public required UIBox2[] SampleRegion { get; init; }
        public required Color[] SampleColor { get; init; }
        public required Vector2[] SampleWorld { get; init; }

        public void Execute(int index)
        {
            SampleValid[index] = false;
            SampleTexture[index] = null;

            var rowIndex = index / LogicalColumns;
            var column = index % LogicalColumns;
            if ((uint) rowIndex >= (uint) Rows.Length)
                return;

            var row = Rows[rowIndex];
            var bandLeft = (int) MathF.Floor(column * Width / (float) LogicalColumns);
            var bandRight = Math.Max(bandLeft + 1, (int) MathF.Ceiling((column + 1) * Width / (float) LogicalColumns));
            var bandWidth = Math.Max(1, bandRight - bandLeft);
            var depthIndex = Math.Clamp(bandLeft + bandWidth / 2, 0, Math.Max(0, DepthBuffer.Length - 1));
            var minDepth = DepthBuffer[depthIndex];

            if (minDepth > 0f && row.Distance >= minDepth - OcclusionEpsilon)
                return;

            var world = row.WorldStart + row.StepVec * column;
            FpvFloorSample floorSample;
            if (!FloorCache.TryGetFloorSample(new MapCoordinates(world, Camera.MapId), world, out floorSample) &&
                !TryGetParallaxFloorSample(world, out floorSample))
            {
                return;
            }

            SampleValid[index] = true;
            SampleTexture[index] = floorSample.Texture;
            SampleRegion[index] = GetFloorSampleRegion(floorSample, row.Distance);
            SampleColor[index] = SceneBuilder.ApplyDistanceFog(Color.White, row.Distance, Camera);
            SampleWorld[index] = world;
        }

        private bool TryGetParallaxFloorSample(Vector2 world, out FpvFloorSample sample)
        {
            sample = default;

            for (var i = 0; i < ParallaxLayers.Length; i++)
            {
                var layer = ParallaxLayers[i];
                if (!layer.Config.Tiled)
                    continue;

                var texture = layer.Texture;
                var scale = layer.Config.Scale;
                if (texture.Width <= 0 || texture.Height <= 0 || scale.X <= 0f || scale.Y <= 0f)
                    continue;

                var sizeWorld = new Vector2(
                    texture.Width / (float) EyeManager.PixelsPerMeter * scale.X,
                    texture.Height / (float) EyeManager.PixelsPerMeter * scale.Y);

                if (sizeWorld.X <= MinNonZero || sizeWorld.Y <= MinNonZero)
                    continue;

                var home = layer.Config.WorldHomePosition;
                var scrolled = layer.Config.Scrolling * (float) RealTime.TotalSeconds;
                var origin = (Camera.EyePos - home) * layer.Config.Slowness;
                origin += home;
                origin += layer.Config.WorldAdjustPosition;
                origin += scrolled;
                origin -= sizeWorld / 2f;

                var local = world - origin;
                var fracX = local.X / sizeWorld.X;
                var fracY = local.Y / sizeWorld.Y;
                fracX -= MathF.Floor(fracX);
                fracY -= MathF.Floor(fracY);

                sample = new FpvFloorSample(
                    texture,
                    new UIBox2(0f, 0f, texture.Width, texture.Height),
                    fracX,
                    fracY);
                return true;
            }

            return false;
        }
    }

    private readonly record struct TransparentSurfaceDraw(Texture? Texture, UIBox2 Rect, UIBox2? Region, Color Color, float Depth, int DrawDepth);

    private readonly record struct AlphaDraw(float Depth, int DrawDepth, AlphaDrawKind Kind, int Index);

    private readonly record struct FloorRowDescriptor(int ScreenY, int ScreenHeight, float Distance, Vector2 WorldStart, Vector2 StepVec);

    private enum AlphaDrawKind
    {
        Surface = 0,
        Billboard = 1
    }
}
