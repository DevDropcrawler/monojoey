namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class LoanManager
{
    private const int FirstBorrowInterestRatePercent = 20;
    private const int SecondBorrowInterestRatePercent = 30;
    private const int ThirdBorrowInterestRatePercent = 50;
    private const int AdditionalBorrowInterestRateStepPercent = 10;
    private const int MaximumInterestRatePercent = 100;

    public static LoanTakeResult TakeLoan(GameState gameState, PlayerId playerId, Money amount)
    {
        var playerIndex = FindPlayerIndexOrNull(gameState.Players, playerId);
        if (playerIndex is null)
        {
            return LoanRejected(
                LoanTakeResultKind.PlayerNotInGame,
                gameState,
                playerId,
                amount,
                loanState: null,
                "Loan player must exist in the game player list.");
        }

        var player = gameState.Players[playerIndex.Value];
        if (player.IsEliminated)
        {
            return LoanRejected(
                LoanTakeResultKind.PlayerEliminated,
                gameState,
                playerId,
                amount,
                player.LoanState,
                "Eliminated players cannot take loans.");
        }

        if (amount.Amount <= 0)
        {
            return LoanRejected(
                LoanTakeResultKind.InvalidAmount,
                gameState,
                playerId,
                amount,
                player.LoanState,
                "Loan amount must be positive.");
        }

        var previousLoanState = player.LoanState;
        var nextLoanTier = (previousLoanState?.LoanTier ?? 0) + 1;
        var totalBorrowed = new Money((previousLoanState?.TotalBorrowed.Amount ?? 0) + amount.Amount);
        var interestRatePercent = CalculateInterestRatePercent(nextLoanTier);
        var loanState = new PlayerLoanState(
            totalBorrowed,
            interestRatePercent,
            CalculateInterestDue(totalBorrowed, interestRatePercent),
            nextLoanTier);

        var players = gameState.Players.ToArray();
        players[playerIndex.Value] = player with
        {
            Money = new Money(player.Money.Amount + amount.Amount),
            LoanState = loanState,
        };

        return new LoanTakeResult(
            LoanTakeResultKind.Accepted,
            gameState with { Players = players },
            playerId,
            amount,
            loanState,
            "Loan accepted.");
    }

    private static LoanTakeResult LoanRejected(
        LoanTakeResultKind resultKind,
        GameState gameState,
        PlayerId playerId,
        Money amount,
        PlayerLoanState? loanState,
        string message)
    {
        return new LoanTakeResult(resultKind, gameState, playerId, amount, loanState, message);
    }

    private static int CalculateInterestRatePercent(int loanTier)
    {
        return loanTier switch
        {
            <= 1 => FirstBorrowInterestRatePercent,
            2 => SecondBorrowInterestRatePercent,
            3 => ThirdBorrowInterestRatePercent,
            _ => Math.Min(
                MaximumInterestRatePercent,
                ThirdBorrowInterestRatePercent + ((loanTier - 3) * AdditionalBorrowInterestRateStepPercent)),
        };
    }

    private static Money CalculateInterestDue(Money totalBorrowed, int interestRatePercent)
    {
        return new Money(totalBorrowed.Amount * interestRatePercent / 100);
    }

    private static int? FindPlayerIndexOrNull(IReadOnlyList<Player> players, PlayerId playerId)
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
