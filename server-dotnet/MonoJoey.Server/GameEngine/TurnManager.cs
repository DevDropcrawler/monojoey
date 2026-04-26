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

        var firstActivePlayer = gameState.Players.FirstOrDefault(player => !player.IsEliminated);
        if (firstActivePlayer is null)
        {
            throw new InvalidOperationException("A game must have at least one active player before turns can start.");
        }

        var startedGameState = gameState with
        {
            CurrentTurnPlayerId = firstActivePlayer.PlayerId,
            Phase = GamePhase.AwaitingRoll,
            TurnNumber = 1,
        };

        return LoanManager.StartTurnInterestCheck(startedGameState, firstActivePlayer.PlayerId);
    }

    public static Player GetCurrentPlayer(GameState gameState)
    {
        if (gameState.CurrentTurnPlayerId is null)
        {
            throw new InvalidOperationException("No current turn player is set.");
        }

        var currentPlayer = gameState.Players.Single(player => player.PlayerId == gameState.CurrentTurnPlayerId.Value);
        if (currentPlayer.IsEliminated)
        {
            throw new InvalidOperationException("Eliminated players cannot take turns.");
        }

        return currentPlayer;
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
        var nextIndex = FindNextActivePlayerIndex(gameState.Players, currentIndex);

        var nextPlayerId = gameState.Players[nextIndex].PlayerId;
        var nextGameState = gameState with
        {
            CurrentTurnPlayerId = nextPlayerId,
            Phase = GamePhase.AwaitingRoll,
            TurnNumber = gameState.TurnNumber + 1,
        };

        return LoanManager.StartTurnInterestCheck(nextGameState, nextPlayerId);
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

    private static int FindNextActivePlayerIndex(IReadOnlyList<Player> players, int currentIndex)
    {
        for (var offset = 1; offset <= players.Count; offset++)
        {
            var nextIndex = (currentIndex + offset) % players.Count;
            if (!players[nextIndex].IsEliminated)
            {
                return nextIndex;
            }
        }

        throw new InvalidOperationException("A game must have at least one active player before turns can advance.");
    }
}
