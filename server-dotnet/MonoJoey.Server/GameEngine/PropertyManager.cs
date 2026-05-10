namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class PropertyManager
{
    private static readonly Money PlaceholderRent = new(10);

    public static GameState AssignOwner(GameState gameState, TileId propertyTileId, PlayerId ownerId)
    {
        _ = FindPurchasableTile(gameState.Board, propertyTileId);
        var ownerIndex = FindPlayerIndex(gameState.Players, ownerId, "Property owner must exist in the game player list.");

        EnsurePropertyIsUnowned(gameState.Players, propertyTileId);

        var players = gameState.Players.ToArray();
        players[ownerIndex] = AddOwnedProperty(players[ownerIndex], propertyTileId);

        return gameState with { Players = players };
    }

    public static PropertyPurchaseResult BuyProperty(GameState gameState, PlayerId buyerId, TileId propertyTileId)
    {
        var propertyTile = FindPurchasableTile(gameState.Board, propertyTileId);
        var buyerIndex = FindPlayerIndex(gameState.Players, buyerId, "Property buyer must exist in the game player list.");

        EnsurePropertyIsUnowned(gameState.Players, propertyTileId);

        if (propertyTile.Price is null)
        {
            throw new InvalidOperationException("Purchasable property must have a price before it can be bought.");
        }

        var price = propertyTile.Price.Value;
        var buyer = gameState.Players[buyerIndex];
        if (buyer.Money.Amount < price.Amount)
        {
            throw new InvalidOperationException("Property buyer must have enough money to buy the property.");
        }

        var players = gameState.Players.ToArray();
        players[buyerIndex] = AddOwnedProperty(buyer with { Money = new Money(buyer.Money.Amount - price.Amount) }, propertyTileId);

        return new PropertyPurchaseResult(gameState with { Players = players }, buyerId, propertyTileId, price);
    }

    public static GameState TransferOwner(
        GameState gameState,
        TileId propertyTileId,
        PlayerId fromOwnerId,
        PlayerId toOwnerId)
    {
        _ = FindPurchasableTile(gameState.Board, propertyTileId);
        var fromOwnerIndex = FindPlayerIndex(
            gameState.Players,
            fromOwnerId,
            "Previous property owner must exist in the game player list.");
        var toOwnerIndex = FindPlayerIndex(
            gameState.Players,
            toOwnerId,
            "New property owner must exist in the game player list.");

        if (fromOwnerId == toOwnerId)
        {
            throw new InvalidOperationException("Property ownership transfer requires distinct players.");
        }

        var currentOwnerIndex = FindPropertyOwnerIndex(gameState.Players, propertyTileId);
        if (currentOwnerIndex is null || currentOwnerIndex.Value != fromOwnerIndex)
        {
            throw new InvalidOperationException("Property must be owned by the previous owner before ownership can be transferred.");
        }

        var players = gameState.Players.ToArray();
        players[fromOwnerIndex] = RemoveOwnedProperty(players[fromOwnerIndex], propertyTileId);
        players[toOwnerIndex] = AddOwnedProperty(players[toOwnerIndex], propertyTileId);

        return gameState with { Players = players };
    }

    public static RentPaymentResult PayRentForCurrentTile(GameState gameState, PlayerId landingPlayerId)
    {
        var assessment = AssessRentForCurrentTile(gameState, landingPlayerId);
        if (!assessment.PaymentRequired || assessment.OwnerId is null)
        {
            return NoRent(gameState, landingPlayerId, assessment.TileId, assessment.OwnerId);
        }

        var landingPlayerIndex = FindPlayerIndex(
            gameState.Players,
            landingPlayerId,
            "Landing player must exist in the game player list before rent can be paid.");
        var ownerIndex = FindPlayerIndex(
            gameState.Players,
            assessment.OwnerId.Value,
            "Property owner must exist in the game player list before rent can be paid.");
        var landingPlayer = gameState.Players[landingPlayerIndex];
        var owner = gameState.Players[ownerIndex];
        var rent = assessment.RentDue;

        if (landingPlayer.Money.Amount < rent.Amount)
        {
            var eliminationResult = BankruptcyManager.EliminateForFailedPayment(gameState, landingPlayerId, rent);
            return new RentPaymentResult(
                eliminationResult.GameState,
                landingPlayerId,
                assessment.TileId,
                owner.PlayerId,
                rent,
                Money.Zero,
                eliminationResult);
        }

        var players = gameState.Players.ToArray();
        players[landingPlayerIndex] = landingPlayer with
        {
            Money = new Money(landingPlayer.Money.Amount - rent.Amount),
        };
        players[ownerIndex] = owner with
        {
            Money = new Money(owner.Money.Amount + rent.Amount),
        };

        return new RentPaymentResult(
            gameState with { Players = players },
            landingPlayerId,
            assessment.TileId,
            owner.PlayerId,
            rent,
            rent);
    }

    public static RentAssessmentResult AssessRentForCurrentTile(GameState gameState, PlayerId landingPlayerId)
    {
        var landingPlayerIndex = FindPlayerIndex(
            gameState.Players,
            landingPlayerId,
            "Landing player must exist in the game player list before rent can be paid.");
        var landingPlayer = gameState.Players[landingPlayerIndex];
        var tile = FindTileById(gameState.Board, landingPlayer.CurrentTileId);

        if (!tile.IsPurchasable)
        {
            return NoRentAssessment(gameState, landingPlayerId, tile.TileId, ownerId: null);
        }

        var ownerIndex = FindPropertyOwnerIndex(gameState.Players, tile.TileId);
        if (ownerIndex is null)
        {
            return NoRentAssessment(gameState, landingPlayerId, tile.TileId, ownerId: null);
        }

        var owner = gameState.Players[ownerIndex.Value];
        if (owner.PlayerId == landingPlayerId)
        {
            return NoRentAssessment(gameState, landingPlayerId, tile.TileId, owner.PlayerId);
        }

        if (IsMortgaged(tile, gameState))
        {
            return NoRentAssessment(gameState, landingPlayerId, tile.TileId, owner.PlayerId);
        }

        var rent = CalculateRent(tile, gameState);
        return new RentAssessmentResult(gameState, landingPlayerId, tile.TileId, owner.PlayerId, rent);
    }

    private static RentPaymentResult NoRent(GameState gameState, PlayerId landingPlayerId, TileId tileId, PlayerId? ownerId)
    {
        return new RentPaymentResult(gameState, landingPlayerId, tileId, ownerId, Money.Zero, Money.Zero);
    }

    private static RentAssessmentResult NoRentAssessment(
        GameState gameState,
        PlayerId landingPlayerId,
        TileId tileId,
        PlayerId? ownerId)
    {
        return new RentAssessmentResult(gameState, landingPlayerId, tileId, ownerId, Money.Zero);
    }

    private static Money CalculateRent(Tile tile, GameState gameState)
    {
        var propertyStateData = gameState.PropertyStates.TryGetValue(tile.TileId, out var propertyState)
            ? propertyState.Data
            : new PropertyStateData();
        var baseRent = GetRentForUpgradeLevel(tile, propertyStateData.UpgradeLevel);
        var damagePercent = propertyStateData.DamagePercent;

        if (damagePercent <= 0)
        {
            return baseRent;
        }

        if (damagePercent >= 100)
        {
            return Money.Zero;
        }

        var multiplier = (100 - damagePercent) / 100m;
        var reduced = (int)Math.Floor(baseRent.Amount * multiplier);

        return new Money(Math.Max(1, reduced));
    }

    private static Money GetRentForUpgradeLevel(Tile tile, int upgradeLevel)
    {
        if (tile.RentTable.Count > upgradeLevel)
        {
            return tile.RentTable[upgradeLevel];
        }

        if (upgradeLevel == 0)
        {
            return PlaceholderRent;
        }

        throw new InvalidOperationException("Property rent table must include rent for the current upgrade level.");
    }

    private static bool IsMortgaged(Tile tile, GameState gameState)
    {
        return gameState.PropertyStates.TryGetValue(tile.TileId, out var propertyState) &&
            propertyState.Data.IsMortgaged;
    }

    private static Player AddOwnedProperty(Player player, TileId propertyTileId)
    {
        var ownedPropertyIds = player.OwnedPropertyIds.ToHashSet();
        ownedPropertyIds.Add(propertyTileId);

        return player with { OwnedPropertyIds = ownedPropertyIds };
    }

    private static Player RemoveOwnedProperty(Player player, TileId propertyTileId)
    {
        var ownedPropertyIds = player.OwnedPropertyIds.ToHashSet();
        ownedPropertyIds.Remove(propertyTileId);

        return player with { OwnedPropertyIds = ownedPropertyIds };
    }

    private static void EnsurePropertyIsUnowned(IReadOnlyList<Player> players, TileId propertyTileId)
    {
        if (FindPropertyOwnerIndex(players, propertyTileId) is not null)
        {
            throw new InvalidOperationException("Property must be unowned before ownership can be assigned.");
        }
    }

    private static int? FindPropertyOwnerIndex(IReadOnlyList<Player> players, TileId propertyTileId)
    {
        int? ownerIndex = null;
        for (var index = 0; index < players.Count; index++)
        {
            if (!players[index].OwnedPropertyIds.Contains(propertyTileId))
            {
                continue;
            }

            if (ownerIndex is not null)
            {
                throw new InvalidOperationException("Property cannot be owned by multiple players.");
            }

            ownerIndex = index;
        }

        return ownerIndex;
    }

    private static int FindPlayerIndex(IReadOnlyList<Player> players, PlayerId playerId, string errorMessage)
    {
        for (var index = 0; index < players.Count; index++)
        {
            if (players[index].PlayerId == playerId)
            {
                return index;
            }
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static Tile FindPurchasableTile(Board board, TileId tileId)
    {
        var tile = FindTileById(board, tileId);
        if (!tile.IsPurchasable)
        {
            throw new InvalidOperationException("Tile must be purchasable before it can be owned.");
        }

        return tile;
    }

    private static Tile FindTileById(Board board, TileId tileId)
    {
        foreach (var tile in board.Tiles)
        {
            if (tile.TileId == tileId)
            {
                return tile;
            }
        }

        throw new InvalidOperationException("Property tile must exist on the board.");
    }
}
