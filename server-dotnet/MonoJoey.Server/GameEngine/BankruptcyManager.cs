namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class BankruptcyManager
{
    public static PlayerEliminationResult EliminateIfBankrupt(GameState gameState, PlayerId playerId)
    {
        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var player = gameState.Players[playerIndex];
        var shouldEliminate = player.Money.Amount < 0;

        return BuildResult(
            gameState,
            playerIndex,
            EliminationReason.NegativeBalance,
            paymentDue: null,
            shouldEliminate);
    }

    public static PlayerEliminationResult EliminateForFailedPayment(
        GameState gameState,
        PlayerId playerId,
        Money paymentDue)
    {
        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var player = gameState.Players[playerIndex];
        var shouldEliminate = player.Money.Amount < 0 || player.Money.Amount < paymentDue.Amount;
        var reason = player.Money.Amount < 0
            ? EliminationReason.NegativeBalance
            : EliminationReason.CannotFulfillPayment;

        return BuildResult(gameState, playerIndex, reason, paymentDue, shouldEliminate);
    }

    private static PlayerEliminationResult BuildResult(
        GameState gameState,
        int playerIndex,
        EliminationReason reason,
        Money? paymentDue,
        bool shouldEliminate)
    {
        var player = gameState.Players[playerIndex];
        if (!shouldEliminate || player.IsEliminated)
        {
            return new PlayerEliminationResult(
                gameState,
                player.PlayerId,
                reason,
                player.Money,
                paymentDue,
                player.Money,
                WasEliminated: false);
        }

        var players = gameState.Players.ToArray();
        players[playerIndex] = player with
        {
            IsBankrupt = true,
            IsEliminated = true,
        };

        return new PlayerEliminationResult(
            gameState with { Players = players },
            player.PlayerId,
            reason,
            player.Money,
            paymentDue,
            player.Money,
            WasEliminated: true);
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

        throw new InvalidOperationException("Player must exist in the game player list before bankruptcy can be resolved.");
    }
}
