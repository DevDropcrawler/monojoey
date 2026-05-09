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

    public static Tile? FindTile(Board board, TileId tileId)
    {
        return board.Tiles.FirstOrDefault(tile => tile.TileId == tileId);
    }

    public static Player? FindPlayer(IReadOnlyList<Player> players, PlayerId playerId)
    {
        return players.FirstOrDefault(player => player.PlayerId == playerId);
    }

    public static PlayerId? FindPropertyOwnerId(IReadOnlyList<Player> players, TileId propertyTileId)
    {
        PlayerId? ownerId = null;
        var ownerCount = 0;
        foreach (var player in players)
        {
            if (!player.OwnedPropertyIds.Contains(propertyTileId))
            {
                continue;
            }

            ownerId = player.PlayerId;
            ownerCount++;
        }

        if (ownerCount > 1)
        {
            throw new InvalidOperationException("Property cannot be owned by multiple players.");
        }

        return ownerId;
    }

    public static PropertyStateData GetPropertyStateData(GameState gameState, TileId tileId)
    {
        return gameState.PropertyStates.TryGetValue(tileId, out var propertyState)
            ? propertyState.Data
            : new PropertyStateData();
    }
}
