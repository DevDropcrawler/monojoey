namespace MonoJoey.Server.GameEngine;

public sealed record PlayerLoanState(
    Money TotalBorrowed,
    int CurrentInterestRatePercent,
    Money NextTurnInterestDue,
    int LoanTier);
