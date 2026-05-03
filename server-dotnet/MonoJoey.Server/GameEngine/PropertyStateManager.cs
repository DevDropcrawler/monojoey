namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class PropertyStateManager
{
    public static GameState ApplyEarthquake(
        GameState gameState,
        IEnumerable<string> tileIds,
        int damagePercent)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(tileIds);

        if (damagePercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(damagePercent),
                "Damage percent must be between 0 and 100.");
        }

        if (damagePercent == 0)
        {
            return gameState;
        }

        var affectedTileIds = tileIds
            .Where(tileId => !string.IsNullOrWhiteSpace(tileId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tileId => tileId, StringComparer.Ordinal)
            .Select(tileId => new TileId(tileId))
            .ToArray();

        if (affectedTileIds.Length == 0)
        {
            return gameState;
        }

        var propertyStates = new Dictionary<TileId, PropertyState>(gameState.PropertyStates);
        var changed = false;

        foreach (var tileId in affectedTileIds)
        {
            if (!IsOwnedPurchasableProperty(gameState, tileId))
            {
                continue;
            }

            var existingDamagePercent = propertyStates.TryGetValue(tileId, out var existingState)
                ? existingState.Data.DamagePercent
                : 0;
            var nextDamagePercent = Math.Max(existingDamagePercent, damagePercent);
            if (nextDamagePercent == existingDamagePercent)
            {
                continue;
            }

            propertyStates[tileId] = new PropertyState(
                tileId,
                new PropertyStateData(nextDamagePercent));
            changed = true;
        }

        return changed
            ? gameState with { PropertyStates = propertyStates }
            : gameState;
    }

    private static bool IsOwnedPurchasableProperty(GameState gameState, TileId tileId)
    {
        var tile = gameState.Board.Tiles.FirstOrDefault(candidate => candidate.TileId == tileId);
        if (tile is null || !tile.IsPurchasable)
        {
            return false;
        }

        return gameState.Players.Any(player => player.OwnedPropertyIds.Contains(tileId));
    }
}
