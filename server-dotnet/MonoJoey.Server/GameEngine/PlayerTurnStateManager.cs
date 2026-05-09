namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class PlayerTurnStateManager
{
    public static GameState ApplyDiceRoll(GameState gameState, PlayerId playerId, bool isDouble)
    {
        return UpdatePlayerTurnState(
            gameState,
            playerId,
            turnState => turnState with
            {
                ConsecutiveDoublesCount = isDouble
                    ? turnState.ConsecutiveDoublesCount + 1
                    : 0,
            });
    }

    public static GameState ResetConsecutiveDoubles(GameState gameState, PlayerId playerId)
    {
        return UpdatePlayerTurnState(
            gameState,
            playerId,
            turnState => turnState with { ConsecutiveDoublesCount = 0 });
    }

    private static GameState UpdatePlayerTurnState(
        GameState gameState,
        PlayerId playerId,
        Func<PlayerTurnState, PlayerTurnState> update)
    {
        var players = gameState.Players.ToArray();
        for (var index = 0; index < players.Length; index++)
        {
            if (players[index].PlayerId != playerId)
            {
                continue;
            }

            players[index] = players[index] with
            {
                TurnState = update(players[index].TurnState),
            };
            return gameState with { Players = players };
        }

        throw new InvalidOperationException("Turn-state player must exist in the game player list.");
    }
}
