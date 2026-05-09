namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class PlayerCashTransferManager
{
    public static PlayerCashTransferResult TransferBetweenPlayers(
        GameState gameState,
        PlayerId fromPlayerId,
        PlayerId toPlayerId,
        Money amount)
    {
        if (fromPlayerId == toPlayerId)
        {
            return Rejected(
                PlayerCashTransferResultKind.SamePlayer,
                gameState,
                fromPlayerId,
                toPlayerId,
                amount,
                "Cash transfers require distinct players.");
        }

        if (amount.Amount <= 0)
        {
            return Rejected(
                PlayerCashTransferResultKind.InvalidAmount,
                gameState,
                fromPlayerId,
                toPlayerId,
                amount,
                "Cash transfer amount must be positive.");
        }

        var fromPlayerIndex = FindPlayerIndex(gameState.Players, fromPlayerId);
        var toPlayerIndex = FindPlayerIndex(gameState.Players, toPlayerId);
        if (fromPlayerIndex is null || toPlayerIndex is null)
        {
            return Rejected(
                PlayerCashTransferResultKind.PlayerNotInGame,
                gameState,
                fromPlayerId,
                toPlayerId,
                amount,
                "Both cash transfer players must exist in the game.");
        }

        var fromPlayer = gameState.Players[fromPlayerIndex.Value];
        var toPlayer = gameState.Players[toPlayerIndex.Value];
        if (fromPlayer.IsBankrupt || toPlayer.IsBankrupt)
        {
            return Rejected(
                PlayerCashTransferResultKind.PlayerBankrupt,
                gameState,
                fromPlayerId,
                toPlayerId,
                amount,
                "Bankrupt players cannot participate in cash transfers.");
        }

        if (fromPlayer.IsEliminated || toPlayer.IsEliminated)
        {
            return Rejected(
                PlayerCashTransferResultKind.PlayerEliminated,
                gameState,
                fromPlayerId,
                toPlayerId,
                amount,
                "Eliminated players cannot participate in cash transfers.");
        }

        if (fromPlayer.Money.Amount < amount.Amount)
        {
            return Rejected(
                PlayerCashTransferResultKind.InsufficientCash,
                gameState,
                fromPlayerId,
                toPlayerId,
                amount,
                "Cash transfer sender does not have enough cash.");
        }

        if (toPlayer.Money.Amount > int.MaxValue - amount.Amount)
        {
            return Rejected(
                PlayerCashTransferResultKind.UnsafeMoneyBalance,
                gameState,
                fromPlayerId,
                toPlayerId,
                amount,
                "Cash transfer would exceed the safe money balance.");
        }

        var players = gameState.Players.ToArray();
        players[fromPlayerIndex.Value] = fromPlayer with
        {
            Money = new Money(fromPlayer.Money.Amount - amount.Amount),
        };
        players[toPlayerIndex.Value] = toPlayer with
        {
            Money = new Money(toPlayer.Money.Amount + amount.Amount),
        };

        return new PlayerCashTransferResult(
            PlayerCashTransferResultKind.Transferred,
            gameState with { Players = players },
            fromPlayerId,
            toPlayerId,
            amount,
            players[fromPlayerIndex.Value].Money,
            players[toPlayerIndex.Value].Money,
            "Cash transferred.");
    }

    private static PlayerCashTransferResult Rejected(
        PlayerCashTransferResultKind kind,
        GameState gameState,
        PlayerId fromPlayerId,
        PlayerId toPlayerId,
        Money amount,
        string message)
    {
        return new PlayerCashTransferResult(
            kind,
            gameState,
            fromPlayerId,
            toPlayerId,
            amount,
            GetPlayerMoney(gameState, fromPlayerId),
            GetPlayerMoney(gameState, toPlayerId),
            message);
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
}
