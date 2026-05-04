namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public static class CardEffectExecutor
{
    private static readonly TileId StartTileId = new("start");

    public static GameState ExecuteCardEffect(GameState gameState, CardResolutionResult cardResolution)
    {
        return ExecuteCardEffectWithResult(gameState, cardResolution).GameState;
    }

    public static CardEffectExecutionResult ExecuteCardEffectWithResult(
        GameState gameState,
        CardResolutionResult cardResolution)
    {
        return cardResolution.ActionKind switch
        {
            CardResolutionActionKind.MoveToStart => MoveToStart(gameState, cardResolution),
            CardResolutionActionKind.MoveToTile => MoveToTile(gameState, cardResolution),
            CardResolutionActionKind.MoveSteps => MoveSteps(gameState, cardResolution),
            CardResolutionActionKind.MoveToNearestTransport => MoveToNearestTileType(
                gameState,
                cardResolution,
                TileType.Transport),
            CardResolutionActionKind.MoveToNearestUtility => MoveToNearestTileType(
                gameState,
                cardResolution,
                TileType.Utility),
            CardResolutionActionKind.ReceiveMoney => ChangeMoney(
                gameState,
                cardResolution.PlayerId,
                RequireAmount(cardResolution)),
            CardResolutionActionKind.PayMoney => PayMoney(
                gameState,
                cardResolution.PlayerId,
                RequireAmount(cardResolution)),
            CardResolutionActionKind.ReceiveMoneyFromEveryPlayer => ReceiveMoneyFromEveryPlayer(
                gameState,
                cardResolution.PlayerId,
                RequireAmount(cardResolution)),
            CardResolutionActionKind.PayMoneyToEveryPlayer => PayMoneyToEveryPlayer(
                gameState,
                cardResolution.PlayerId,
                RequireAmount(cardResolution)),
            CardResolutionActionKind.RepairOwnedProperties => PayMoney(
                gameState,
                cardResolution.PlayerId,
                CalculatePropertyRepairCost(gameState, cardResolution.PlayerId, RequireAmount(cardResolution))),
            CardResolutionActionKind.ApplySlimer => new CardEffectExecutionResult(
                PlayerStatusEffectManager.ApplySlimer(
                    gameState,
                    cardResolution.PlayerId,
                    sourceId: cardResolution.CardId.Value)),
            CardResolutionActionKind.ApplyEarthquake => new CardEffectExecutionResult(
                PropertyStateManager.ApplyEarthquake(
                    gameState,
                    RequireTileIds(cardResolution).Select(tileId => tileId.Value),
                    RequireDamagePercent(cardResolution))),
            CardResolutionActionKind.GoToLockup => new CardEffectExecutionResult(
                LockupManager.SendToLockup(gameState, cardResolution.PlayerId)),
            CardResolutionActionKind.GetOutOfLockup => new CardEffectExecutionResult(
                LockupManager.GrantGetOutOfLockupEscape(
                    gameState,
                    cardResolution.PlayerId,
                    cardResolution.CardId)),
            _ => new CardEffectExecutionResult(gameState),
        };
    }

    private static CardEffectExecutionResult MoveToStart(GameState gameState, CardResolutionResult cardResolution)
    {
        var steps = CalculateForwardStepsToTile(gameState, cardResolution.PlayerId, StartTileId);
        var movement = MovementManager.MovePlayer(gameState, cardResolution.PlayerId, steps);

        return new CardEffectExecutionResult(movement.GameState, movement);
    }

    private static CardEffectExecutionResult MoveSteps(GameState gameState, CardResolutionResult cardResolution)
    {
        var steps = cardResolution.Parameters?.StepCount
            ?? throw new InvalidOperationException("Resolved card movement must include a step count.");
        var movement = MovementManager.MovePlayer(gameState, cardResolution.PlayerId, steps);

        return new CardEffectExecutionResult(movement.GameState, movement);
    }

    private static CardEffectExecutionResult MoveToTile(GameState gameState, CardResolutionResult cardResolution)
    {
        var targetTileId = cardResolution.Parameters?.TargetTileId
            ?? throw new InvalidOperationException("Resolved card movement must include a target tile.");
        var steps = CalculateForwardStepsToTile(gameState, cardResolution.PlayerId, targetTileId);
        var movement = MovementManager.MovePlayer(gameState, cardResolution.PlayerId, steps);

        return new CardEffectExecutionResult(movement.GameState, movement);
    }

    private static CardEffectExecutionResult MoveToNearestTileType(
        GameState gameState,
        CardResolutionResult cardResolution,
        TileType targetTileType)
    {
        var targetTile = FindNearestTileByType(gameState, cardResolution.PlayerId, targetTileType);
        var steps = CalculateForwardStepsToTile(gameState, cardResolution.PlayerId, targetTile.TileId);
        var movement = MovementManager.MovePlayer(gameState, cardResolution.PlayerId, steps);

        return new CardEffectExecutionResult(movement.GameState, movement);
    }

    private static CardEffectExecutionResult PayMoney(GameState gameState, PlayerId playerId, Money amount)
    {
        var paidGameState = ChangeMoney(gameState, playerId, new Money(-amount.Amount)).GameState;

        return new CardEffectExecutionResult(BankruptcyManager.EliminateIfBankrupt(paidGameState, playerId).GameState);
    }

    private static CardEffectExecutionResult ReceiveMoneyFromEveryPlayer(GameState gameState, PlayerId playerId, Money amount)
    {
        var receiverIndex = FindPlayerIndex(gameState.Players, playerId);
        var players = gameState.Players.ToArray();

        for (var index = 0; index < players.Length; index++)
        {
            var payer = players[index];
            if (index == receiverIndex || payer.IsEliminated)
            {
                continue;
            }

            if (payer.Money.Amount < amount.Amount)
            {
                var eliminatedState = BankruptcyManager.EliminateForFailedPayment(
                    gameState with { Players = players },
                    payer.PlayerId,
                    amount).GameState;
                players = eliminatedState.Players.ToArray();
                continue;
            }

            players[index] = payer with { Money = new Money(payer.Money.Amount - amount.Amount) };
            var receiver = players[receiverIndex];
            players[receiverIndex] = receiver with { Money = new Money(receiver.Money.Amount + amount.Amount) };
        }

        return new CardEffectExecutionResult(gameState with { Players = players });
    }

    private static CardEffectExecutionResult PayMoneyToEveryPlayer(GameState gameState, PlayerId playerId, Money amount)
    {
        var payerIndex = FindPlayerIndex(gameState.Players, playerId);
        var activeOpponentCount = gameState.Players.Count(player => player.PlayerId != playerId && !player.IsEliminated);
        var totalDue = new Money(amount.Amount * activeOpponentCount);
        var payer = gameState.Players[payerIndex];
        if (payer.Money.Amount < totalDue.Amount)
        {
            return new CardEffectExecutionResult(BankruptcyManager.EliminateForFailedPayment(gameState, playerId, totalDue).GameState);
        }

        var players = gameState.Players.ToArray();
        players[payerIndex] = payer with { Money = new Money(payer.Money.Amount - totalDue.Amount) };
        for (var index = 0; index < players.Length; index++)
        {
            if (index == payerIndex || players[index].IsEliminated)
            {
                continue;
            }

            players[index] = players[index] with { Money = new Money(players[index].Money.Amount + amount.Amount) };
        }

        return new CardEffectExecutionResult(gameState with { Players = players });
    }

    private static Money CalculatePropertyRepairCost(GameState gameState, PlayerId playerId, Money amountPerProperty)
    {
        var player = gameState.Players[FindPlayerIndex(gameState.Players, playerId)];

        return new Money(amountPerProperty.Amount * player.OwnedPropertyIds.Count);
    }

    private static CardEffectExecutionResult ChangeMoney(GameState gameState, PlayerId playerId, Money delta)
    {
        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var player = gameState.Players[playerIndex];
        var players = gameState.Players.ToArray();
        players[playerIndex] = player with
        {
            Money = new Money(player.Money.Amount + delta.Amount),
        };

        return new CardEffectExecutionResult(gameState with { Players = players });
    }

    private static Money RequireAmount(CardResolutionResult cardResolution)
    {
        return cardResolution.Parameters?.Amount
            ?? throw new InvalidOperationException("Resolved card money effect must include an amount.");
    }

    private static IReadOnlyList<TileId> RequireTileIds(CardResolutionResult cardResolution)
    {
        return cardResolution.Parameters?.TileIds
            ?? throw new InvalidOperationException("Resolved card earthquake effect must include tile IDs.");
    }

    private static int RequireDamagePercent(CardResolutionResult cardResolution)
    {
        return cardResolution.Parameters?.DamagePercent
            ?? throw new InvalidOperationException("Resolved card earthquake effect must include damage percent.");
    }

    private static int CalculateForwardStepsToTile(
        GameState gameState,
        PlayerId playerId,
        TileId targetTileId)
    {
        if (gameState.Board.Tiles.Count == 0)
        {
            throw new InvalidOperationException("A board must have at least one tile before players can move.");
        }

        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var currentTile = FindTileById(gameState.Board, gameState.Players[playerIndex].CurrentTileId);
        var targetTile = FindTileById(gameState.Board, targetTileId);

        return PositiveModulo(targetTile.Index - currentTile.Index, gameState.Board.Tiles.Count);
    }

    private static int PositiveModulo(int value, int divisor)
    {
        return ((value % divisor) + divisor) % divisor;
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

        throw new InvalidOperationException("Card effect player must exist in the game player list.");
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

        throw new InvalidOperationException("Card movement target tile must exist on the board.");
    }

    private static Tile FindNearestTileByType(GameState gameState, PlayerId playerId, TileType targetTileType)
    {
        if (gameState.Board.Tiles.Count == 0)
        {
            throw new InvalidOperationException("A board must have at least one tile before card movement can resolve.");
        }

        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var currentTile = FindTileById(gameState.Board, gameState.Players[playerIndex].CurrentTileId);

        for (var offset = 1; offset <= gameState.Board.Tiles.Count; offset++)
        {
            var targetIndex = PositiveModulo(currentTile.Index + offset, gameState.Board.Tiles.Count);
            foreach (var tile in gameState.Board.Tiles)
            {
                if (tile.Index == targetIndex && tile.TileType == targetTileType)
                {
                    return tile;
                }
            }
        }

        throw new InvalidOperationException("Card movement target tile type must exist on the board.");
    }
}

public sealed record CardEffectExecutionResult(
    GameState GameState,
    MovementResult? MovementResult = null);
