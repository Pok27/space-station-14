using System.Numerics;
using System.Collections.Generic;
using Content.Client.Resources;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Client.E3D.FirstPerson;

public sealed class FirstPersonFloorCacheSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private readonly Dictionary<(EntityUid GridUid, Vector2i Indices), CachedFloorTile?> _tileCache = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        foreach (var change in args.Changes)
        {
            _tileCache.Remove((args.Entity.Owner, change.GridIndices));
        }
    }

    public bool TryGetFloorSample(MapCoordinates coordinates, Vector2 world, out FpvFloorSample sample)
    {
        var lookup = default(FloorSampleLookup);
        return TryGetFloorSample(coordinates.MapId, world, ref lookup, out sample);
    }

    public bool TryGetFloorSample(MapId mapId, Vector2 world, ref FloorSampleLookup lookup, out FpvFloorSample sample)
    {
        sample = default;

        if (lookup.TryGetCached(this, mapId, world, out sample))
            return true;

        var coordinates = new MapCoordinates(world, mapId);
        if (!_mapManager.TryFindGridAt(coordinates, out var gridUid, out var grid))
        {
            lookup.Reset();
            return false;
        }

        var indices = _map.TileIndicesFor(gridUid, grid, coordinates);
        var tile = GetOrCreateTile(gridUid, grid, indices);
        if (tile == null)
        {
            lookup.Update(mapId, gridUid, indices, false, default!, default, 0);
            return false;
        }

        var local = _map.WorldToLocal(gridUid, grid, world);
        sample = BuildSample(grid.TileSize, local, tile.Value);
        lookup.Update(mapId, gridUid, indices, true, tile.Value.Texture, tile.Value.TextureRegion, tile.Value.RotationMirroring);
        return true;
    }

    private CachedFloorTile? GetOrCreateTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        if (_tileCache.TryGetValue((gridUid, indices), out var cached))
            return cached;

        if (!_map.TryGetTileRef(gridUid, grid, indices, out var tileRef))
            return _tileCache[(gridUid, indices)] = null;

        var def = (ContentTileDefinition) _tileDefs[tileRef.Tile.TypeId];
        if (def.Sprite == null)
            return _tileCache[(gridUid, indices)] = null;

        var texture = _resources.GetTexture(def.Sprite.Value);
        var tileSize = texture.Height;
        var variant = Math.Clamp(tileRef.Tile.Variant, 0, Math.Max(0, def.Variants - 1));
        var region = new UIBox2(variant * tileSize, 0, (variant + 1) * tileSize, tileSize);

        cached = new CachedFloorTile(texture, region, tileRef.Tile.RotationMirroring);
        _tileCache[(gridUid, indices)] = cached;
        return cached;
    }

    private static void ApplyTileRotationMirroring(byte rotationMirroring, ref float fracX, ref float fracY)
    {
        if (rotationMirroring > 3)
            fracX = 1f - fracX;

        var rotation = rotationMirroring % 4;
        for (var i = 0; i < rotation; i++)
            (fracX, fracY) = (fracY, 1f - fracX);
    }

    private static FpvFloorSample BuildSample(float tileSize, Vector2 local, CachedFloorTile tile)
    {
        var cellX = local.X / tileSize;
        var cellY = local.Y / tileSize;
        var fracX = cellX - MathF.Floor(cellX);
        var fracY = cellY - MathF.Floor(cellY);
        ApplyTileRotationMirroring(tile.RotationMirroring, ref fracX, ref fracY);

        return new FpvFloorSample(tile.Texture, tile.TextureRegion, fracX, fracY);
    }

    public struct FloorSampleLookup
    {
        private MapId _mapId;
        private EntityUid _gridUid;
        private Vector2i _tileIndices;
        private Texture _texture;
        private UIBox2 _region;
        private byte _rotationMirroring;
        private bool _hasCachedResult;
        private bool _hasTile;

        public void Reset()
        {
            _gridUid = EntityUid.Invalid;
            _hasCachedResult = false;
            _hasTile = false;
        }

        public void Update(MapId mapId, EntityUid gridUid, Vector2i tileIndices, bool hasTile, Texture texture, UIBox2 region, byte rotationMirroring)
        {
            _mapId = mapId;
            _gridUid = gridUid;
            _tileIndices = tileIndices;
            _hasCachedResult = true;
            _hasTile = hasTile;
            _texture = texture;
            _region = region;
            _rotationMirroring = rotationMirroring;
        }

        public bool TryGetCached(FirstPersonFloorCacheSystem system, MapId mapId, Vector2 world, out FpvFloorSample sample)
        {
            sample = default;
            if (!_hasCachedResult || _mapId != mapId || _gridUid == EntityUid.Invalid)
                return false;

            if (!system.TryComp(_gridUid, out MapGridComponent? grid))
                return false;

            var local = system._map.WorldToLocal(_gridUid, grid, world);
            var tileX = (int) MathF.Floor(local.X / grid.TileSize);
            var tileY = (int) MathF.Floor(local.Y / grid.TileSize);
            if (tileX != _tileIndices.X || tileY != _tileIndices.Y)
                return false;

            if (!_hasTile)
                return false;

            sample = BuildSample(grid.TileSize, local, _texture, _region, _rotationMirroring);
            return true;
        }
    }

    private static FpvFloorSample BuildSample(float tileSize, Vector2 local, Texture texture, UIBox2 region, byte rotationMirroring)
    {
        var cellX = local.X / tileSize;
        var cellY = local.Y / tileSize;
        var fracX = cellX - MathF.Floor(cellX);
        var fracY = cellY - MathF.Floor(cellY);
        ApplyTileRotationMirroring(rotationMirroring, ref fracX, ref fracY);
        return new FpvFloorSample(texture, region, fracX, fracY);
    }

    private readonly record struct CachedFloorTile(Texture Texture, UIBox2 TextureRegion, byte RotationMirroring);
}
