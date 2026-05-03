namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class LoanManager
{
    private const int MaximumInterestRatePercent = 100;

    public static GameState StartTurnInterestCheck(GameState gameState, PlayerId playerId, LoanSharkConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var player = gameState.Players[playerIndex];
        var loanState = player.LoanState;
        if (loanState is null)
        {
            return gameState;
        }

        var interestDue = CalculateInterestDue(loanState.TotalBorrowed, loanState.CurrentInterestRatePercent);
        var players = gameState.Players.ToArray();
        players[playerIndex] = player with
        {
            Money = new Money(player.Money.Amount - interestDue.Amount),
            LoanState = loanState with
            {
                NextTurnInterestDue = interestDue,
            },
        };

        var paidGameState = gameState with { Players = players };
        return BankruptcyManager.EliminateIfBankrupt(paidGameState, playerId).GameState;
    }

    public static LoanTakeResult TakeLoan(
        GameState gameState,
        PlayerId playerId,
        Money amount,
        BorrowPurpose purpose,
        LoanSharkConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var playerIndex = FindPlayerIndexOrNull(gameState.Players, playerId);
        if (playerIndex is null)
        {
            return LoanRejected(
                LoanTakeResultKind.PlayerNotInGame,
                gameState,
                playerId,
                amount,
                purpose,
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
                purpose,
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
                purpose,
                player.LoanState,
                "Loan amount must be positive.");
        }

        if (!IsBorrowPurposeAllowed(purpose, config))
        {
            return LoanRejected(
                LoanTakeResultKind.DisallowedBorrowPurpose,
                gameState,
                playerId,
                amount,
                purpose,
                player.LoanState,
                "Players cannot borrow to pay loan interest or loan debt.");
        }

        var previousLoanState = player.LoanState;
        var nextLoanTier = (previousLoanState?.LoanTier ?? 0) + 1;
        var totalBorrowed = new Money((previousLoanState?.TotalBorrowed.Amount ?? 0) + amount.Amount);
        var interestRatePercent = CalculateInterestRatePercent(nextLoanTier, config);
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
            purpose,
            loanState,
            "Loan accepted.");
    }

    private static LoanTakeResult LoanRejected(
        LoanTakeResultKind resultKind,
        GameState gameState,
        PlayerId playerId,
        Money amount,
        BorrowPurpose purpose,
        PlayerLoanState? loanState,
        string message)
    {
        return new LoanTakeResult(resultKind, gameState, playerId, amount, purpose, loanState, message);
    }

    private static bool IsBorrowPurposeAllowed(BorrowPurpose purpose, LoanSharkConfig config)
    {
        return purpose switch
        {
            BorrowPurpose.AuctionBid
                or BorrowPurpose.RentPayment
                or BorrowPurpose.TaxPayment
                or BorrowPurpose.CardPenalty
                or BorrowPurpose.Fine => true,
            BorrowPurpose.LoanInterest
                or BorrowPurpose.LoanPrincipalRepayment
                or BorrowPurpose.ExistingLoanDebt => config.CanBorrowForLoanPayments,
            _ => false,
        };
    }

    private static int CalculateInterestRatePercent(int loanTier, LoanSharkConfig config)
    {
        return loanTier switch
        {
            <= 1 => config.FirstBorrowInterestRatePercent,
            2 => config.SecondBorrowInterestRatePercent,
            3 => config.ThirdBorrowInterestRatePercent,
            _ => Math.Min(
                MaximumInterestRatePercent,
                config.ThirdBorrowInterestRatePercent +
                    ((loanTier - 3) * config.AdditionalBorrowInterestRateStepPercent)),
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

    private static int FindPlayerIndex(IReadOnlyList<Player> players, PlayerId playerId)
    {
        for (var index = 0; index < players.Count; index++)
        {
            if (players[index].PlayerId == playerId)
            {
                return index;
            }
        }

        throw new InvalidOperationException("Loan player must exist in the game player list.");
    }
}
