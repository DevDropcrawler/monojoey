namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public static class MortgageManager
{
    public static MortgageResult MortgageProperty(
        GameState gameState,
        PlayerId playerId,
        TileId propertyTileId)
    {
        var validation = ValidateRequest(gameState, playerId, propertyTileId);
        if (validation is not null)
        {
            return MortgageRejected(
                MapMortgageValidation(validation.Value.Kind),
                gameState,
                playerId,
                propertyTileId,
                validation.Value.Message);
        }

        var playerIndex = FindPlayerIndex(gameState.Players, playerId).GetValueOrDefault();
        var player = gameState.Players[playerIndex];
        var tile = FindTile(gameState.Board, propertyTileId)
            ?? throw new InvalidOperationException("Validated mortgage property must exist.");
        var currentData = GetPropertyStateData(gameState, propertyTileId);
        if (currentData.UpgradeLevel > 0)
        {
            return MortgageRejected(
                MortgageResultKind.PropertyHasUpgrades,
                gameState,
                playerId,
                propertyTileId,
                "Upgraded properties cannot be mortgaged.");
        }

        if (currentData.IsMortgaged)
        {
            return MortgageRejected(
                MortgageResultKind.AlreadyMortgaged,
                gameState,
                playerId,
                propertyTileId,
                "Property is already mortgaged.");
        }

        var mortgageValue = EconomyRulesCalculator.CalculateMortgageValue(tile.Price!.Value, gameState.Rules.Economy);
        if (player.Money.Amount > int.MaxValue - mortgageValue.Amount)
        {
            return MortgageRejected(
                MortgageResultKind.UnsafeMoneyBalance,
                gameState,
                playerId,
                propertyTileId,
                "Mortgage would exceed the safe money balance.");
        }

        var players = gameState.Players.ToArray();
        players[playerIndex] = player with
        {
            Money = new Money(player.Money.Amount + mortgageValue.Amount),
        };

        var propertyStates = new Dictionary<TileId, PropertyState>(gameState.PropertyStates)
        {
            [propertyTileId] = new(
                propertyTileId,
                new PropertyStateData(
                    currentData.DamagePercent,
                    isMortgaged: true,
                    upgradeLevel: currentData.UpgradeLevel)),
        };
        var updatedGameState = gameState with
        {
            Players = players,
            PropertyStates = propertyStates,
        };

        return new MortgageResult(
            MortgageResultKind.Mortgaged,
            updatedGameState,
            playerId,
            propertyTileId,
            mortgageValue,
            players[playerIndex].Money,
            IsMortgaged: true,
            "Property mortgaged.");
    }

    public static UnmortgageResult UnmortgageProperty(
        GameState gameState,
        PlayerId playerId,
        TileId propertyTileId)
    {
        var validation = ValidateRequest(gameState, playerId, propertyTileId);
        if (validation is not null)
        {
            return UnmortgageRejected(
                MapUnmortgageValidation(validation.Value.Kind),
                gameState,
                playerId,
                propertyTileId,
                validation.Value.Message);
        }

        var playerIndex = FindPlayerIndex(gameState.Players, playerId).GetValueOrDefault();
        var player = gameState.Players[playerIndex];
        var tile = FindTile(gameState.Board, propertyTileId)
            ?? throw new InvalidOperationException("Validated unmortgage property must exist.");
        var currentData = GetPropertyStateData(gameState, propertyTileId);
        if (!currentData.IsMortgaged)
        {
            return UnmortgageRejected(
                UnmortgageResultKind.NotMortgaged,
                gameState,
                playerId,
                propertyTileId,
                "Property is not mortgaged.");
        }

        var mortgageValue = EconomyRulesCalculator.CalculateMortgageValue(tile.Price!.Value, gameState.Rules.Economy);
        var unmortgageInterest = EconomyRulesCalculator.CalculateUnmortgageInterest(mortgageValue, gameState.Rules.Economy);
        if (mortgageValue.Amount > int.MaxValue - unmortgageInterest.Amount)
        {
            return UnmortgageRejected(
                UnmortgageResultKind.UnsafeMoneyBalance,
                gameState,
                playerId,
                propertyTileId,
                "Unmortgage cost exceeds the safe money balance.");
        }

        var unmortgageCost = new Money(mortgageValue.Amount + unmortgageInterest.Amount);
        if (player.Money.Amount < unmortgageCost.Amount)
        {
            return UnmortgageRejected(
                UnmortgageResultKind.InsufficientCash,
                gameState,
                playerId,
                propertyTileId,
                "Player does not have enough cash to unmortgage the property.",
                mortgageValue,
                unmortgageInterest,
                unmortgageCost);
        }

        var players = gameState.Players.ToArray();
        players[playerIndex] = player with
        {
            Money = new Money(player.Money.Amount - unmortgageCost.Amount),
        };

        var propertyStates = new Dictionary<TileId, PropertyState>(gameState.PropertyStates);
        if (currentData.DamagePercent > 0 || currentData.UpgradeLevel > 0)
        {
            propertyStates[propertyTileId] = new PropertyState(
                propertyTileId,
                new PropertyStateData(
                    currentData.DamagePercent,
                    isMortgaged: false,
                    upgradeLevel: currentData.UpgradeLevel));
        }
        else
        {
            propertyStates.Remove(propertyTileId);
        }

        var updatedGameState = gameState with
        {
            Players = players,
            PropertyStates = propertyStates,
        };

        return new UnmortgageResult(
            UnmortgageResultKind.Unmortgaged,
            updatedGameState,
            playerId,
            propertyTileId,
            mortgageValue,
            unmortgageInterest,
            unmortgageCost,
            players[playerIndex].Money,
            IsMortgaged: false,
            "Property unmortgaged.");
    }

    private static MortgageResult MortgageRejected(
        MortgageResultKind kind,
        GameState gameState,
        PlayerId playerId,
        TileId propertyTileId,
        string message)
    {
        return new MortgageResult(
            kind,
            gameState,
            playerId,
            propertyTileId,
            Money.Zero,
            GetPlayerMoney(gameState, playerId),
            IsMortgaged: GetPropertyStateData(gameState, propertyTileId).IsMortgaged,
            message);
    }

    private static UnmortgageResult UnmortgageRejected(
        UnmortgageResultKind kind,
        GameState gameState,
        PlayerId playerId,
        TileId propertyTileId,
        string message,
        Money? mortgageValue = null,
        Money? unmortgageInterest = null,
        Money? unmortgageCost = null)
    {
        return new UnmortgageResult(
            kind,
            gameState,
            playerId,
            propertyTileId,
            mortgageValue ?? Money.Zero,
            unmortgageInterest ?? Money.Zero,
            unmortgageCost ?? Money.Zero,
            GetPlayerMoney(gameState, playerId),
            IsMortgaged: GetPropertyStateData(gameState, propertyTileId).IsMortgaged,
            message);
    }

    private static RequestValidation? ValidateRequest(
        GameState gameState,
        PlayerId playerId,
        TileId propertyTileId)
    {
        if (!gameState.Rules.Economy.MortgagesEnabled)
        {
            return new RequestValidation(ValidationKind.MortgagesDisabled, "Mortgages are disabled.");
        }

        if (gameState.Status != GameStatus.InProgress)
        {
            return new RequestValidation(ValidationKind.GameNotInProgress, "Game is not in progress.");
        }

        if (gameState.ActiveAuctionState is not null)
        {
            return new RequestValidation(ValidationKind.ActiveAuction, "Mortgages are blocked during active auctions.");
        }

        if (HasUnresolvedTileExecution(gameState))
        {
            return new RequestValidation(
                ValidationKind.UnresolvedTileExecution,
                "Mortgages are blocked while the current tile is awaiting execution.");
        }

        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        if (playerIndex is null)
        {
            return new RequestValidation(ValidationKind.PlayerNotInGame, "Player is not in the game.");
        }

        var player = gameState.Players[playerIndex.Value];
        if (player.IsEliminated)
        {
            return new RequestValidation(ValidationKind.PlayerEliminated, "Eliminated players cannot mortgage properties.");
        }

        var tile = FindTile(gameState.Board, propertyTileId);
        if (tile is null || !tile.IsPurchasable || tile.Price is null)
        {
            return new RequestValidation(ValidationKind.InvalidProperty, "Property tile is not a purchasable property.");
        }

        if (!player.OwnedPropertyIds.Contains(propertyTileId))
        {
            return new RequestValidation(ValidationKind.PropertyNotOwned, "Player does not own this property.");
        }

        return null;
    }

    private static bool HasUnresolvedTileExecution(GameState gameState)
    {
        return gameState.CurrentTurnPlayerId is not null &&
            gameState.HasRolledThisTurn &&
            gameState.HasResolvedTileThisTurn &&
            !gameState.HasExecutedTileThisTurn;
    }

    private static PropertyStateData GetPropertyStateData(GameState gameState, TileId tileId)
    {
        return gameState.PropertyStates.TryGetValue(tileId, out var propertyState)
            ? propertyState.Data
            : new PropertyStateData();
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

    private static MortgageResultKind MapMortgageValidation(ValidationKind kind)
    {
        return kind switch
        {
            ValidationKind.MortgagesDisabled => MortgageResultKind.MortgagesDisabled,
            ValidationKind.PlayerNotInGame => MortgageResultKind.PlayerNotInGame,
            ValidationKind.PlayerEliminated => MortgageResultKind.PlayerEliminated,
            ValidationKind.GameNotInProgress => MortgageResultKind.GameNotInProgress,
            ValidationKind.ActiveAuction => MortgageResultKind.ActiveAuction,
            ValidationKind.UnresolvedTileExecution => MortgageResultKind.UnresolvedTileExecution,
            ValidationKind.InvalidProperty => MortgageResultKind.InvalidProperty,
            ValidationKind.PropertyNotOwned => MortgageResultKind.PropertyNotOwned,
            _ => throw new InvalidOperationException("Unknown mortgage validation kind."),
        };
    }

    private static UnmortgageResultKind MapUnmortgageValidation(ValidationKind kind)
    {
        return kind switch
        {
            ValidationKind.MortgagesDisabled => UnmortgageResultKind.MortgagesDisabled,
            ValidationKind.PlayerNotInGame => UnmortgageResultKind.PlayerNotInGame,
            ValidationKind.PlayerEliminated => UnmortgageResultKind.PlayerEliminated,
            ValidationKind.GameNotInProgress => UnmortgageResultKind.GameNotInProgress,
            ValidationKind.ActiveAuction => UnmortgageResultKind.ActiveAuction,
            ValidationKind.UnresolvedTileExecution => UnmortgageResultKind.UnresolvedTileExecution,
            ValidationKind.InvalidProperty => UnmortgageResultKind.InvalidProperty,
            ValidationKind.PropertyNotOwned => UnmortgageResultKind.PropertyNotOwned,
            _ => throw new InvalidOperationException("Unknown unmortgage validation kind."),
        };
    }

    private readonly record struct RequestValidation(ValidationKind Kind, string Message);

    private enum ValidationKind
    {
        MortgagesDisabled,
        PlayerNotInGame,
        PlayerEliminated,
        GameNotInProgress,
        ActiveAuction,
        UnresolvedTileExecution,
        InvalidProperty,
        PropertyNotOwned,
    }
}
