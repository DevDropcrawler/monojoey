namespace MonoJoey.Server.GameEngine;

public sealed record LoanSharkConfig
{
    public bool Enabled { get; init; } = true;

    public int FirstBorrowInterestRatePercent { get; init; } = 20;

    public int SecondBorrowInterestRatePercent { get; init; } = 30;

    public int ThirdBorrowInterestRatePercent { get; init; } = 50;

    public int AdditionalBorrowInterestRateStepPercent { get; init; } = 10;

    public bool CanBorrowForLoanPayments { get; init; } = false;

    public static LoanSharkConfig FromRules(LoanRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return new LoanSharkConfig
        {
            Enabled = rules.LoanSharkEnabled,
            CanBorrowForLoanPayments = rules.CanBorrowForLoanPayments,
        };
    }
}
