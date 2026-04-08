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
        sample = default;

        if (!_mapManager.TryFindGridAt(coordinates, out var gridUid, out var grid))
            return false;

        var indices = _map.TileIndicesFor(gridUid, grid, coordinates);
        var tile = GetOrCreateTile(gridUid, grid, indices);
        if (tile == null)
            return false;

        var local = _map.WorldToLocal(gridUid, grid, world);
        var cellX = local.X / grid.TileSize;
        var cellY = local.Y / grid.TileSize;
        var fracX = cellX - MathF.Floor(cellX);
        var fracY = cellY - MathF.Floor(cellY);
        ApplyTileRotationMirroring(tile.Value.RotationMirroring, ref fracX, ref fracY);

        sample = new FpvFloorSample(
            tile.Value.Texture,
            tile.Value.TextureRegion,
            fracX,
            fracY);

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

    private readonly record struct CachedFloorTile(Texture Texture, UIBox2 TextureRegion, byte RotationMirroring);
}
