namespace MonoJoey.Server.GameEngine;

public sealed record LoanSharkConfig
{
    public bool Enabled { get; init; } = true;

    public int FirstBorrowInterestRatePercent { get; init; } = 20;

    public int SecondBorrowInterestRatePercent { get; init; } = 30;

    public int ThirdBorrowInterestRatePercent { get; init; } = 50;

    public int AdditionalBorrowInterestRateStepPercent { get; init; } = 10;

    public int MinimumInterestPayment { get; init; }

    public bool CanBorrowForLoanPayments { get; init; } = false;

    public static LoanSharkConfig FromRules(LoanRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return new LoanSharkConfig
        {
            Enabled = rules.LoanSharkEnabled,
            FirstBorrowInterestRatePercent = ToPercent(rules.BaseInterestRate),
            SecondBorrowInterestRatePercent = ToPercent(rules.BaseInterestRate + rules.InterestRateIncreasePerLoan),
            ThirdBorrowInterestRatePercent = ToPercent(
                rules.BaseInterestRate +
                    rules.InterestRateIncreasePerLoan +
                    rules.InterestRateIncreasePerDebtTier),
            AdditionalBorrowInterestRateStepPercent = ToPercent(rules.InterestRateIncreasePerLoan),
            MinimumInterestPayment = rules.MinimumInterestPayment,
            CanBorrowForLoanPayments = rules.CanBorrowForLoanPayments,
        };
    }

    private static int ToPercent(decimal rate)
    {
        return Math.Min(100, (int)Math.Round(rate * 100m, MidpointRounding.AwayFromZero));
    }
}
