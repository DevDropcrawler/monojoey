namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class PropertyStateManager
{
    private const int StartTurnRepairPercent = 10;

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

    public static GameState RepairDamagedOwnedProperties(GameState gameState, PlayerId ownerId)
    {
        ArgumentNullException.ThrowIfNull(gameState);

        var ownerIndex = FindPlayerIndex(gameState.Players, ownerId);
        var owner = gameState.Players[ownerIndex];
        if (owner.IsEliminated || gameState.PropertyStates.Count == 0)
        {
            return gameState;
        }

        var damagedOwnedProperties = gameState.PropertyStates
            .Where(propertyState => propertyState.Value.Data.DamagePercent > 0)
            .Where(propertyState => owner.OwnedPropertyIds.Contains(propertyState.Key))
            .OrderBy(propertyState => propertyState.Key.Value, StringComparer.Ordinal)
            .ToArray();

        if (damagedOwnedProperties.Length == 0)
        {
            return gameState;
        }

        var boardTilesById = gameState.Board.Tiles.ToDictionary(tile => tile.TileId);
        var players = gameState.Players.ToArray();
        var propertyStates = new Dictionary<TileId, PropertyState>(gameState.PropertyStates);
        var changed = false;

        foreach (var (tileId, propertyState) in damagedOwnedProperties)
        {
            if (!boardTilesById.TryGetValue(tileId, out var tile) ||
                !tile.IsPurchasable ||
                tile.Price is null)
            {
                continue;
            }

            var currentOwner = players[ownerIndex];
            var damagePercent = propertyState.Data.DamagePercent;
            var repairedPercent = Math.Min(StartTurnRepairPercent, damagePercent);
            var repairCost = Math.Max(
                1,
                (int)Math.Floor(tile.Price.Value.Amount * repairedPercent / 100m));

            if (currentOwner.Money.Amount < repairCost)
            {
                break;
            }

            players[ownerIndex] = currentOwner with
            {
                Money = new Money(currentOwner.Money.Amount - repairCost),
            };

            var nextDamagePercent = damagePercent - repairedPercent;
            if (nextDamagePercent <= 0)
            {
                propertyStates.Remove(tileId);
            }
            else
            {
                propertyStates[tileId] = propertyState with
                {
                    Data = new PropertyStateData(nextDamagePercent),
                };
            }

            changed = true;
        }

        return changed
            ? gameState with
            {
                Players = players,
                PropertyStates = propertyStates,
            }
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

    private static int FindPlayerIndex(IReadOnlyList<Player> players, PlayerId playerId)
    {
        for (var index = 0; index < players.Count; index++)
        {
            if (players[index].PlayerId == playerId)
            {
                return index;
            }
        }

        throw new InvalidOperationException("Property repair owner must exist in the game player list.");
    }
}
