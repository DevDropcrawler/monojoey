namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class CardEffectExecutor
{
    private static readonly TileId StartTileId = new("start");

    public static GameState ExecuteCardEffect(GameState gameState, CardResolutionResult cardResolution)
    {
        return cardResolution.ActionKind switch
        {
            CardResolutionActionKind.MoveToStart => MoveToStart(gameState, cardResolution),
            CardResolutionActionKind.MoveSteps => MoveSteps(gameState, cardResolution),
            CardResolutionActionKind.ReceiveMoney => ChangeMoney(
                gameState,
                cardResolution.PlayerId,
                RequireAmount(cardResolution)),
            CardResolutionActionKind.PayMoney => PayMoney(
                gameState,
                cardResolution.PlayerId,
                RequireAmount(cardResolution)),
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

    private static GameState PayMoney(GameState gameState, PlayerId playerId, Money amount)
    {
        var paidGameState = ChangeMoney(gameState, playerId, new Money(-amount.Amount));

        return BankruptcyManager.EliminateIfBankrupt(paidGameState, playerId).GameState;
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
}
