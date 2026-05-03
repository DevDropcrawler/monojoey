namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Schemas;

public static class GameCompletionManager
{
    public static GameState CompleteIfWinner(GameState persistedGameState, DateTimeOffset endedAtUtc)
    {
        if (persistedGameState.Status == GameStatus.Completed)
        {
            return persistedGameState;
        }

        if (persistedGameState.Rules.Win.ConditionType != WinRules.LastPlayerStandingConditionType)
        {
            return persistedGameState;
        }

        var activePlayers = persistedGameState.Players
            .Where(player => !player.IsBankrupt && !player.IsEliminated)
            .ToArray();

        if (activePlayers.Length != 1)
        {
            return persistedGameState;
        }

        return persistedGameState with
        {
            Status = GameStatus.Completed,
            Phase = GamePhase.Completed,
            WinnerPlayerId = activePlayers[0].PlayerId,
            EndedAtUtc = endedAtUtc,
            ActiveAuctionState = null,
        };
    }
}
