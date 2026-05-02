namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public static class CardEffectExecutor
{
    private static readonly TileId StartTileId = new("start");

    public static GameState ExecuteCardEffect(GameState gameState, CardResolutionResult cardResolution)
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
            CardResolutionActionKind.GoToLockup => LockupManager.SendToLockup(
                gameState,
                cardResolution.PlayerId),
            CardResolutionActionKind.GetOutOfLockup => LockupManager.GrantGetOutOfLockupEscape(
                gameState,
                cardResolution.PlayerId,
                cardResolution.CardId),
            _ => gameState,
        };
    }

    private static GameState MoveToStart(GameState gameState, CardResolutionResult cardResolution)
    {
        var steps = CalculateForwardStepsToTile(gameState, cardResolution.PlayerId, StartTileId);

        return MovementManager.MovePlayer(gameState, cardResolution.PlayerId, steps).GameState;
    }

    private static GameState MoveSteps(GameState gameState, CardResolutionResult cardResolution)
    {
        var steps = cardResolution.Parameters?.StepCount
            ?? throw new InvalidOperationException("Resolved card movement must include a step count.");

        return MovementManager.MovePlayer(gameState, cardResolution.PlayerId, steps).GameState;
    }

    private static GameState MoveToTile(GameState gameState, CardResolutionResult cardResolution)
    {
        var targetTileId = cardResolution.Parameters?.TargetTileId
            ?? throw new InvalidOperationException("Resolved card movement must include a target tile.");
        var steps = CalculateForwardStepsToTile(gameState, cardResolution.PlayerId, targetTileId);

        return MovementManager.MovePlayer(gameState, cardResolution.PlayerId, steps).GameState;
    }

    private static GameState MoveToNearestTileType(
        GameState gameState,
        CardResolutionResult cardResolution,
        TileType targetTileType)
    {
        var targetTile = FindNearestTileByType(gameState, cardResolution.PlayerId, targetTileType);
        var steps = CalculateForwardStepsToTile(gameState, cardResolution.PlayerId, targetTile.TileId);

        return MovementManager.MovePlayer(gameState, cardResolution.PlayerId, steps).GameState;
    }

    private static GameState PayMoney(GameState gameState, PlayerId playerId, Money amount)
    {
        var paidGameState = ChangeMoney(gameState, playerId, new Money(-amount.Amount));

        return BankruptcyManager.EliminateIfBankrupt(paidGameState, playerId).GameState;
    }

    private static GameState ReceiveMoneyFromEveryPlayer(GameState gameState, PlayerId playerId, Money amount)
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

        return gameState with { Players = players };
    }

    private static GameState PayMoneyToEveryPlayer(GameState gameState, PlayerId playerId, Money amount)
    {
        var payerIndex = FindPlayerIndex(gameState.Players, playerId);
        var activeOpponentCount = gameState.Players.Count(player => player.PlayerId != playerId && !player.IsEliminated);
        var totalDue = new Money(amount.Amount * activeOpponentCount);
        var payer = gameState.Players[payerIndex];
        if (payer.Money.Amount < totalDue.Amount)
        {
            return BankruptcyManager.EliminateForFailedPayment(gameState, playerId, totalDue).GameState;
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

        return gameState with { Players = players };
    }

    private static Money CalculatePropertyRepairCost(GameState gameState, PlayerId playerId, Money amountPerProperty)
    {
        var player = gameState.Players[FindPlayerIndex(gameState.Players, playerId)];

        return new Money(amountPerProperty.Amount * player.OwnedPropertyIds.Count);
    }

    private static GameState ChangeMoney(GameState gameState, PlayerId playerId, Money delta)
    {
        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var player = gameState.Players[playerIndex];
        var players = gameState.Players.ToArray();
        players[playerIndex] = player with
        {
            Money = new Money(player.Money.Amount + delta.Amount),
        };

        return gameState with { Players = players };
    }

    private static Money RequireAmount(CardResolutionResult cardResolution)
    {
        return cardResolution.Parameters?.Amount
            ?? throw new InvalidOperationException("Resolved card money effect must include an amount.");
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
