namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public static class PropertyUpgradeManager
{
    private const int MaximumUpgradeLevel = 5;

    public static PropertyUpgradeResult BuyUpgrade(
        GameState gameState,
        PlayerId playerId,
        TileId propertyTileId)
    {
        var validation = ValidateRequest(gameState, playerId, propertyTileId);
        if (validation is not null)
        {
            return Rejected(
                validation.Value.Kind,
                gameState,
                playerId,
                propertyTileId,
                validation.Value.Message);
        }

        var playerIndex = FindPlayerIndex(gameState.Players, playerId).GetValueOrDefault();
        var player = gameState.Players[playerIndex];
        var tile = FindTile(gameState.Board, propertyTileId)
            ?? throw new InvalidOperationException("Validated upgrade property must exist.");
        var currentData = PropertyRuleHelpers.GetPropertyStateData(gameState, propertyTileId);
        if (currentData.UpgradeLevel >= MaximumUpgradeLevel)
        {
            return Rejected(
                PropertyUpgradeResultKind.MaximumUpgradeLevel,
                gameState,
                playerId,
                propertyTileId,
                "Property is already at the maximum upgrade level.");
        }

        var nextUpgradeLevel = currentData.UpgradeLevel + 1;
        if (!HasRentTier(tile, nextUpgradeLevel))
        {
            return Rejected(
                PropertyUpgradeResultKind.RentTierUnavailable,
                gameState,
                playerId,
                propertyTileId,
                "Property rent table does not support the next upgrade level.");
        }

        if (!CanBuildEvenly(gameState, tile, currentData.UpgradeLevel))
        {
            return Rejected(
                PropertyUpgradeResultKind.UnevenUpgrade,
                gameState,
                playerId,
                propertyTileId,
                "Properties in a group must be upgraded evenly.");
        }

        var upgradeCost = tile.UpgradeCost!.Value;
        if (player.Money.Amount < upgradeCost.Amount)
        {
            return Rejected(
                PropertyUpgradeResultKind.InsufficientCash,
                gameState,
                playerId,
                propertyTileId,
                "Player does not have enough cash to buy an upgrade.",
                upgradeCost: upgradeCost);
        }

        var players = gameState.Players.ToArray();
        players[playerIndex] = player with
        {
            Money = new Money(player.Money.Amount - upgradeCost.Amount),
        };

        var propertyStates = new Dictionary<TileId, PropertyState>(gameState.PropertyStates)
        {
            [propertyTileId] = new(
                propertyTileId,
                new PropertyStateData(
                    currentData.DamagePercent,
                    currentData.IsMortgaged,
                    nextUpgradeLevel)),
        };

        return new PropertyUpgradeResult(
            PropertyUpgradeResultKind.UpgradeBought,
            gameState with
            {
                Players = players,
                PropertyStates = propertyStates,
            },
            playerId,
            propertyTileId,
            nextUpgradeLevel,
            upgradeCost,
            Money.Zero,
            players[playerIndex].Money,
            "Property upgrade bought.");
    }

    public static PropertyUpgradeResult SellUpgrade(
        GameState gameState,
        PlayerId playerId,
        TileId propertyTileId)
    {
        var validation = ValidateRequest(gameState, playerId, propertyTileId);
        if (validation is not null)
        {
            return Rejected(
                validation.Value.Kind,
                gameState,
                playerId,
                propertyTileId,
                validation.Value.Message);
        }

        var playerIndex = FindPlayerIndex(gameState.Players, playerId).GetValueOrDefault();
        var player = gameState.Players[playerIndex];
        var tile = FindTile(gameState.Board, propertyTileId)
            ?? throw new InvalidOperationException("Validated upgrade property must exist.");
        var currentData = PropertyRuleHelpers.GetPropertyStateData(gameState, propertyTileId);
        if (currentData.UpgradeLevel <= 0)
        {
            return Rejected(
                PropertyUpgradeResultKind.MinimumUpgradeLevel,
                gameState,
                playerId,
                propertyTileId,
                "Property has no upgrades to sell.");
        }

        if (!CanSellEvenly(gameState, tile, currentData.UpgradeLevel))
        {
            return Rejected(
                PropertyUpgradeResultKind.UnevenDowngrade,
                gameState,
                playerId,
                propertyTileId,
                "Properties in a group must be downgraded evenly.");
        }

        var upgradeCost = tile.UpgradeCost!.Value;
        var refundAmount = EconomyRulesCalculator.CalculateUpgradeSellRefund(upgradeCost, gameState.Rules.Economy);
        if (player.Money.Amount > int.MaxValue - refundAmount.Amount)
        {
            return Rejected(
                PropertyUpgradeResultKind.UnsafeMoneyBalance,
                gameState,
                playerId,
                propertyTileId,
                "Upgrade sale refund would exceed the safe money balance.",
                upgradeCost,
                refundAmount);
        }

        var players = gameState.Players.ToArray();
        players[playerIndex] = player with
        {
            Money = new Money(player.Money.Amount + refundAmount.Amount),
        };

        var nextUpgradeLevel = currentData.UpgradeLevel - 1;
        var propertyStates = new Dictionary<TileId, PropertyState>(gameState.PropertyStates);
        if (currentData.DamagePercent == 0 && !currentData.IsMortgaged && nextUpgradeLevel == 0)
        {
            propertyStates.Remove(propertyTileId);
        }
        else
        {
            propertyStates[propertyTileId] = new PropertyState(
                propertyTileId,
                new PropertyStateData(
                    currentData.DamagePercent,
                    currentData.IsMortgaged,
                    nextUpgradeLevel));
        }

        return new PropertyUpgradeResult(
            PropertyUpgradeResultKind.UpgradeSold,
            gameState with
            {
                Players = players,
                PropertyStates = propertyStates,
            },
            playerId,
            propertyTileId,
            nextUpgradeLevel,
            upgradeCost,
            refundAmount,
            players[playerIndex].Money,
            "Property upgrade sold.");
    }

    private static RequestValidation? ValidateRequest(
        GameState gameState,
        PlayerId playerId,
        TileId propertyTileId)
    {
        if (!gameState.Rules.Economy.UpgradesEnabled)
        {
            return new RequestValidation(PropertyUpgradeResultKind.UpgradesDisabled, "Property upgrades are disabled.");
        }

        if (gameState.Status != GameStatus.InProgress)
        {
            return new RequestValidation(PropertyUpgradeResultKind.GameNotInProgress, "Game is not in progress.");
        }

        if (gameState.ActiveAuctionState is not null)
        {
            return new RequestValidation(PropertyUpgradeResultKind.ActiveAuction, "Property upgrades are blocked during active auctions.");
        }

        if (HasUnresolvedTileExecution(gameState))
        {
            return new RequestValidation(
                PropertyUpgradeResultKind.UnresolvedTileExecution,
                "Property upgrades are blocked while the current tile is awaiting execution.");
        }

        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        if (playerIndex is null)
        {
            return new RequestValidation(PropertyUpgradeResultKind.PlayerNotInGame, "Player is not in the game.");
        }

        var player = gameState.Players[playerIndex.Value];
        if (player.IsBankrupt)
        {
            return new RequestValidation(PropertyUpgradeResultKind.PlayerBankrupt, "Bankrupt players cannot upgrade properties.");
        }

        if (player.IsEliminated)
        {
            return new RequestValidation(PropertyUpgradeResultKind.PlayerEliminated, "Eliminated players cannot upgrade properties.");
        }

        var tile = FindTile(gameState.Board, propertyTileId);
        if (tile is null || !PropertyRuleHelpers.IsBuildableProperty(tile))
        {
            return new RequestValidation(PropertyUpgradeResultKind.InvalidProperty, "Property tile is not buildable.");
        }

        if (!player.OwnedPropertyIds.Contains(propertyTileId))
        {
            return new RequestValidation(PropertyUpgradeResultKind.PropertyNotOwned, "Player does not own this property.");
        }

        var groupTiles = PropertyRuleHelpers.GetBuildableGroupTiles(gameState.Board, tile);
        if (groupTiles.Count < 2 || groupTiles.Any(groupTile => !player.OwnedPropertyIds.Contains(groupTile.TileId)))
        {
            return new RequestValidation(
                PropertyUpgradeResultKind.IncompleteGroupOwnership,
                "Player must own the full buildable property group before upgrades can change.");
        }

        if (groupTiles.Any(groupTile => PropertyRuleHelpers.GetPropertyStateData(gameState, groupTile.TileId).IsMortgaged))
        {
            return new RequestValidation(
                PropertyUpgradeResultKind.MortgagedGroupProperty,
                "Property upgrades are blocked while any group property is mortgaged.");
        }

        return null;
    }

    private static bool CanBuildEvenly(GameState gameState, Tile tile, int currentUpgradeLevel)
    {
        var groupLevels = PropertyRuleHelpers.GetBuildableGroupTiles(gameState.Board, tile)
            .Select(groupTile => PropertyRuleHelpers.GetPropertyStateData(gameState, groupTile.TileId).UpgradeLevel)
            .ToArray();

        return groupLevels.Length > 0 && currentUpgradeLevel == groupLevels.Min();
    }

    private static bool CanSellEvenly(GameState gameState, Tile tile, int currentUpgradeLevel)
    {
        var groupLevels = PropertyRuleHelpers.GetBuildableGroupTiles(gameState.Board, tile)
            .Select(groupTile => PropertyRuleHelpers.GetPropertyStateData(gameState, groupTile.TileId).UpgradeLevel)
            .ToArray();

        return groupLevels.Length > 0 && currentUpgradeLevel == groupLevels.Max();
    }

    private static bool HasRentTier(Tile tile, int upgradeLevel)
    {
        return tile.RentTable.Count > upgradeLevel;
    }

    private static bool HasUnresolvedTileExecution(GameState gameState)
    {
        return gameState.CurrentTurnPlayerId is not null &&
            gameState.HasRolledThisTurn &&
            gameState.HasResolvedTileThisTurn &&
            !gameState.HasExecutedTileThisTurn;
    }

    private static PropertyUpgradeResult Rejected(
        PropertyUpgradeResultKind kind,
        GameState gameState,
        PlayerId playerId,
        TileId propertyTileId,
        string message,
        Money? upgradeCost = null,
        Money? refundAmount = null)
    {
        return new PropertyUpgradeResult(
            kind,
            gameState,
            playerId,
            propertyTileId,
            PropertyRuleHelpers.GetPropertyStateData(gameState, propertyTileId).UpgradeLevel,
            upgradeCost ?? Money.Zero,
            refundAmount ?? Money.Zero,
            GetPlayerMoney(gameState, playerId),
            message);
    }

    private static Money GetPlayerMoney(GameState gameState, PlayerId playerId)
    {
        return gameState.Players.FirstOrDefault(player => player.PlayerId == playerId)?.Money ?? Money.Zero;
    }

    private static int? FindPlayerIndex(IReadOnlyList<Player> players, PlayerId playerId)
    {
        for (var index = 0; index < players.Count; index++)
        {
            if (players[index].PlayerId == playerId)
            {
                return index;
            }
        }

        return null;
    }

    private static Tile? FindTile(Board board, TileId tileId)
    {
        return board.Tiles.FirstOrDefault(tile => tile.TileId == tileId);
    }

    private readonly record struct RequestValidation(PropertyUpgradeResultKind Kind, string Message);
}
