namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public static class TurnManager
{
    public static GameState StartFirstTurn(GameState gameState)
    {
        if (gameState.Players.Count == 0)
        {
            throw new InvalidOperationException("A game must have at least one player before turns can start.");
        }

        return gameState with
        {
            CurrentTurnPlayerId = gameState.Players[0].PlayerId,
            Phase = GamePhase.AwaitingRoll,
            TurnNumber = 1,
        };
    }

    public static Player GetCurrentPlayer(GameState gameState)
    {
        if (gameState.CurrentTurnPlayerId is null)
        {
            throw new InvalidOperationException("No current turn player is set.");
        }

        return gameState.Players.Single(player => player.PlayerId == gameState.CurrentTurnPlayerId.Value);
    }

    public static GameState AdvanceToNextTurn(GameState gameState)
    {
        if (gameState.Players.Count == 0)
        {
            throw new InvalidOperationException("A game must have at least one player before turns can advance.");
        }

        if (gameState.CurrentTurnPlayerId is null)
        {
            return StartFirstTurn(gameState);
        }

        var currentIndex = FindCurrentPlayerIndex(gameState.Players, gameState.CurrentTurnPlayerId.Value);
        var nextIndex = (currentIndex + 1) % gameState.Players.Count;

        return gameState with
        {
            CurrentTurnPlayerId = gameState.Players[nextIndex].PlayerId,
            Phase = GamePhase.AwaitingRoll,
            TurnNumber = gameState.TurnNumber + 1,
        };
    }

    private static int FindCurrentPlayerIndex(IReadOnlyList<Player> players, PlayerId currentTurnPlayerId)
    {
        for (var index = 0; index < players.Count; index++)
        {
            if (players[index].PlayerId == currentTurnPlayerId)
            {
                return index;
            }
        }

        throw new InvalidOperationException("Current turn player must exist in the game player list.");
    }
}
