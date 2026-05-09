namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public static class TradeManager
{
    public static TradeValidationResult ValidateTrade(
        GameState gameState,
        PlayerId firstPlayerId,
        TradeAssets firstPlayerAssets,
        PlayerId secondPlayerId,
        TradeAssets secondPlayerAssets)
    {
        firstPlayerAssets = NormalizeAssets(firstPlayerAssets);
        secondPlayerAssets = NormalizeAssets(secondPlayerAssets);

        var validation = ValidateTradeCore(
            gameState,
            firstPlayerId,
            firstPlayerAssets,
            secondPlayerId,
            secondPlayerAssets);

        return validation is null
            ? new TradeValidationResult(
                TradeSettlementResultKind.Settled,
                firstPlayerId,
                secondPlayerId,
                firstPlayerAssets,
                secondPlayerAssets,
                "Trade is valid.")
            : new TradeValidationResult(
                validation.Value.Kind,
                firstPlayerId,
                secondPlayerId,
                firstPlayerAssets,
                secondPlayerAssets,
                validation.Value.Message);
    }

    public static TradeSettlementResult SettleTrade(
        GameState gameState,
        PlayerId firstPlayerId,
        TradeAssets firstPlayerAssets,
        PlayerId secondPlayerId,
        TradeAssets secondPlayerAssets)
    {
        var validation = ValidateTrade(
            gameState,
            firstPlayerId,
            firstPlayerAssets,
            secondPlayerId,
            secondPlayerAssets);
        firstPlayerAssets = validation.FirstPlayerAssets;
        secondPlayerAssets = validation.SecondPlayerAssets;

        if (!validation.TradeValid)
        {
            return Rejected(
                validation.ResultKind,
                gameState,
                firstPlayerId,
                firstPlayerAssets,
                secondPlayerId,
                secondPlayerAssets,
                validation.Message);
        }

        var currentState = gameState;
        var cashTransferResults = new List<PlayerCashTransferResult>();
        var ownershipChanges = new List<PropertyOwnershipChange>();

        if (firstPlayerAssets.Cash.Amount > 0)
        {
            var transfer = PlayerCashTransferManager.TransferBetweenPlayers(
                currentState,
                firstPlayerId,
                secondPlayerId,
                firstPlayerAssets.Cash);
            if (!transfer.TransferAccepted)
            {
                return Rejected(
                    MapCashTransferFailure(transfer.ResultKind),
                    gameState,
                    firstPlayerId,
                    firstPlayerAssets,
                    secondPlayerId,
                    secondPlayerAssets,
                    transfer.Message);
            }

            currentState = transfer.GameState;
            cashTransferResults.Add(transfer);
        }

        if (secondPlayerAssets.Cash.Amount > 0)
        {
            var transfer = PlayerCashTransferManager.TransferBetweenPlayers(
                currentState,
                secondPlayerId,
                firstPlayerId,
                secondPlayerAssets.Cash);
            if (!transfer.TransferAccepted)
            {
                return Rejected(
                    MapCashTransferFailure(transfer.ResultKind),
                    gameState,
                    firstPlayerId,
                    firstPlayerAssets,
                    secondPlayerId,
                    secondPlayerAssets,
                    transfer.Message);
            }

            currentState = transfer.GameState;
            cashTransferResults.Add(transfer);
        }

        foreach (var propertyTileId in SortPropertyIds(firstPlayerAssets.PropertyTileIds))
        {
            currentState = PropertyManager.TransferOwner(
                currentState,
                propertyTileId,
                firstPlayerId,
                secondPlayerId);
            ownershipChanges.Add(new PropertyOwnershipChange(propertyTileId, firstPlayerId, secondPlayerId));
        }

        foreach (var propertyTileId in SortPropertyIds(secondPlayerAssets.PropertyTileIds))
        {
            currentState = PropertyManager.TransferOwner(
                currentState,
                propertyTileId,
                secondPlayerId,
                firstPlayerId);
            ownershipChanges.Add(new PropertyOwnershipChange(propertyTileId, secondPlayerId, firstPlayerId));
        }

        return new TradeSettlementResult(
            TradeSettlementResultKind.Settled,
            currentState,
            firstPlayerId,
            secondPlayerId,
            firstPlayerAssets,
            secondPlayerAssets,
            cashTransferResults,
            ownershipChanges,
            "Trade settled.");
    }

    private static RequestValidation? ValidateTradeCore(
        GameState gameState,
        PlayerId firstPlayerId,
        TradeAssets firstPlayerAssets,
        PlayerId secondPlayerId,
        TradeAssets secondPlayerAssets)
    {
        if (gameState.Status != GameStatus.InProgress)
        {
            return new RequestValidation(TradeSettlementResultKind.GameNotInProgress, "Game is not in progress.");
        }

        if (gameState.ActiveAuctionState is not null)
        {
            return new RequestValidation(TradeSettlementResultKind.ActiveAuction, "Trades are blocked during active auctions.");
        }

        if (HasUnresolvedTileExecution(gameState))
        {
            return new RequestValidation(
                TradeSettlementResultKind.UnresolvedTileExecution,
                "Trades are blocked while the current tile is awaiting execution.");
        }

        if (firstPlayerId == secondPlayerId)
        {
            return new RequestValidation(TradeSettlementResultKind.SamePlayer, "Trades require distinct players.");
        }

        var firstPlayer = FindPlayer(gameState.Players, firstPlayerId);
        var secondPlayer = FindPlayer(gameState.Players, secondPlayerId);
        if (firstPlayer is null || secondPlayer is null)
        {
            return new RequestValidation(TradeSettlementResultKind.PlayerNotInGame, "Both trade players must exist in the game.");
        }

        if (firstPlayer.IsBankrupt || secondPlayer.IsBankrupt)
        {
            return new RequestValidation(TradeSettlementResultKind.PlayerBankrupt, "Bankrupt players cannot trade.");
        }

        if (firstPlayer.IsEliminated || secondPlayer.IsEliminated)
        {
            return new RequestValidation(TradeSettlementResultKind.PlayerEliminated, "Eliminated players cannot trade.");
        }

        if (firstPlayerAssets.Cash.Amount < 0 || secondPlayerAssets.Cash.Amount < 0)
        {
            return new RequestValidation(TradeSettlementResultKind.NegativeCash, "Trade cash amounts must be non-negative.");
        }

        if (firstPlayerAssets.Cash.Amount == 0 &&
            secondPlayerAssets.Cash.Amount == 0 &&
            firstPlayerAssets.PropertyTileIds.Count == 0 &&
            secondPlayerAssets.PropertyTileIds.Count == 0)
        {
            return new RequestValidation(TradeSettlementResultKind.EmptyTrade, "At least one trade side must offer cash or property.");
        }

        if (HasDuplicateProperty(firstPlayerAssets.PropertyTileIds) || HasDuplicateProperty(secondPlayerAssets.PropertyTileIds))
        {
            return new RequestValidation(TradeSettlementResultKind.DuplicateProperty, "A trade side cannot offer the same property more than once.");
        }

        if (PropertiesOverlap(firstPlayerAssets.PropertyTileIds, secondPlayerAssets.PropertyTileIds))
        {
            return new RequestValidation(TradeSettlementResultKind.PropertyOfferedByBothSides, "The same property cannot be offered by both sides.");
        }

        var propertyValidation = ValidateProperties(gameState, firstPlayerId, firstPlayerAssets.PropertyTileIds);
        if (propertyValidation is not null)
        {
            return propertyValidation;
        }

        propertyValidation = ValidateProperties(gameState, secondPlayerId, secondPlayerAssets.PropertyTileIds);
        if (propertyValidation is not null)
        {
            return propertyValidation;
        }

        var firstFinalBalance = (long)firstPlayer.Money.Amount - firstPlayerAssets.Cash.Amount + secondPlayerAssets.Cash.Amount;
        var secondFinalBalance = (long)secondPlayer.Money.Amount - secondPlayerAssets.Cash.Amount + firstPlayerAssets.Cash.Amount;
        if (firstFinalBalance < 0 || secondFinalBalance < 0)
        {
            return new RequestValidation(TradeSettlementResultKind.InsufficientCash, "Trade cash settlement is not affordable.");
        }

        if (firstFinalBalance > int.MaxValue || secondFinalBalance > int.MaxValue)
        {
            return new RequestValidation(TradeSettlementResultKind.UnsafeMoneyBalance, "Trade cash settlement would exceed the safe money balance.");
        }

        return null;
    }

    private static RequestValidation? ValidateProperties(
        GameState gameState,
        PlayerId offeringPlayerId,
        IReadOnlyList<TileId> propertyTileIds)
    {
        foreach (var propertyTileId in propertyTileIds)
        {
            var tile = FindTile(gameState.Board, propertyTileId);
            if (tile is null || !tile.IsPurchasable)
            {
                return new RequestValidation(TradeSettlementResultKind.InvalidProperty, "Trade property must be a purchasable board tile.");
            }

            if (PropertyRuleHelpers.HasUpgrades(gameState, propertyTileId))
            {
                return new RequestValidation(
                    TradeSettlementResultKind.PropertyHasUpgrades,
                    "Upgraded properties cannot be traded.");
            }

            var owner = FindPropertyOwner(gameState.Players, propertyTileId);
            if (owner.OwnedByMultiplePlayers)
            {
                return new RequestValidation(
                    TradeSettlementResultKind.PropertyOwnedByMultiplePlayers,
                    "Trade property cannot be owned by multiple players.");
            }

            if (owner.PlayerId is null || owner.PlayerId.Value != offeringPlayerId)
            {
                return new RequestValidation(
                    TradeSettlementResultKind.PropertyNotOwnedByOfferingPlayer,
                    "Trade property must be owned by the offering player.");
            }
        }

        return null;
    }

    private static TradeAssets NormalizeAssets(TradeAssets assets)
    {
        return assets with { PropertyTileIds = assets.PropertyTileIds.ToArray() };
    }

    private static TradeSettlementResult Rejected(
        TradeSettlementResultKind kind,
        GameState gameState,
        PlayerId firstPlayerId,
        TradeAssets firstPlayerAssets,
        PlayerId secondPlayerId,
        TradeAssets secondPlayerAssets,
        string message)
    {
        return new TradeSettlementResult(
            kind,
            gameState,
            firstPlayerId,
            secondPlayerId,
            firstPlayerAssets,
            secondPlayerAssets,
            Array.Empty<PlayerCashTransferResult>(),
            Array.Empty<PropertyOwnershipChange>(),
            message);
    }

    private static TradeSettlementResultKind MapCashTransferFailure(PlayerCashTransferResultKind kind)
    {
        return kind switch
        {
            PlayerCashTransferResultKind.SamePlayer => TradeSettlementResultKind.SamePlayer,
            PlayerCashTransferResultKind.PlayerNotInGame => TradeSettlementResultKind.PlayerNotInGame,
            PlayerCashTransferResultKind.PlayerBankrupt => TradeSettlementResultKind.PlayerBankrupt,
            PlayerCashTransferResultKind.PlayerEliminated => TradeSettlementResultKind.PlayerEliminated,
            PlayerCashTransferResultKind.InvalidAmount => TradeSettlementResultKind.NegativeCash,
            PlayerCashTransferResultKind.InsufficientCash => TradeSettlementResultKind.InsufficientCash,
            PlayerCashTransferResultKind.UnsafeMoneyBalance => TradeSettlementResultKind.UnsafeMoneyBalance,
            _ => throw new InvalidOperationException("Accepted cash transfer cannot be mapped to a trade failure."),
        };
    }

    private static bool HasUnresolvedTileExecution(GameState gameState)
    {
        return gameState.CurrentTurnPlayerId is not null &&
            gameState.HasRolledThisTurn &&
            gameState.HasResolvedTileThisTurn &&
            !gameState.HasExecutedTileThisTurn;
    }

    private static bool HasDuplicateProperty(IReadOnlyList<TileId> propertyTileIds)
    {
        return propertyTileIds.Count != propertyTileIds.Distinct().Count();
    }

    private static bool PropertiesOverlap(IReadOnlyList<TileId> firstPropertyTileIds, IReadOnlyList<TileId> secondPropertyTileIds)
    {
        return firstPropertyTileIds.Intersect(secondPropertyTileIds).Any();
    }

    private static IReadOnlyList<TileId> SortPropertyIds(IReadOnlyList<TileId> propertyTileIds)
    {
        return propertyTileIds.OrderBy(propertyTileId => propertyTileId.Value, StringComparer.Ordinal).ToArray();
    }

    private static Player? FindPlayer(IReadOnlyList<Player> players, PlayerId playerId)
    {
        return players.FirstOrDefault(player => player.PlayerId == playerId);
    }

    private static Tile? FindTile(Board board, TileId tileId)
    {
        return board.Tiles.FirstOrDefault(tile => tile.TileId == tileId);
    }

    private static PropertyOwnerLookup FindPropertyOwner(IReadOnlyList<Player> players, TileId propertyTileId)
    {
        PlayerId? ownerId = null;
        var ownedCount = 0;
        foreach (var player in players)
        {
            if (!player.OwnedPropertyIds.Contains(propertyTileId))
            {
                continue;
            }

            ownerId = player.PlayerId;
            ownedCount++;
        }

        return new PropertyOwnerLookup(ownerId, ownedCount > 1);
    }

    private readonly record struct RequestValidation(TradeSettlementResultKind Kind, string Message);

    private readonly record struct PropertyOwnerLookup(PlayerId? PlayerId, bool OwnedByMultiplePlayers);
}
