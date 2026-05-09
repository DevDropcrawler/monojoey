namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public static class PropertyRuleHelpers
{
    public static bool IsBuildableProperty(Tile tile)
    {
        return tile.TileType == TileType.Property &&
            tile.IsPurchasable &&
            tile.UpgradeCost is not null &&
            !string.IsNullOrWhiteSpace(tile.GroupId) &&
            tile.RentTable.Count > 1;
    }

    public static IReadOnlyList<Tile> GetBuildableGroupTiles(Board board, Tile tile)
    {
        if (!IsBuildableProperty(tile) || string.IsNullOrWhiteSpace(tile.GroupId))
        {
            return Array.Empty<Tile>();
        }

        return board.Tiles
            .Where(candidate => string.Equals(candidate.GroupId, tile.GroupId, StringComparison.Ordinal))
            .Where(IsBuildableProperty)
            .OrderBy(candidate => candidate.Index)
            .ThenBy(candidate => candidate.TileId.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool HasUpgrades(GameState gameState, TileId tileId)
    {
        return gameState.PropertyStates.TryGetValue(tileId, out var propertyState) &&
            propertyState.Data.UpgradeLevel > 0;
    }

    public static PropertyStateData GetPropertyStateData(GameState gameState, TileId tileId)
    {
        return gameState.PropertyStates.TryGetValue(tileId, out var propertyState)
            ? propertyState.Data
            : new PropertyStateData();
    }
}
