namespace MonoJoey.Server.GameEngine;

public static class GameRulesPresets
{
    public const string MonoJoeyDefaultPresetId = "monojoey_default";
    public const string CustomPresetId = "custom";

    public static GameRules MonoJoeyDefault => new(
        Version: 1,
        PresetId: MonoJoeyDefaultPresetId,
        PresetName: "MonoJoey default",
        IsCustom: false,
        Economy: new EconomyRules(
            StartingMoney: 1500,
            PassStartReward: 200,
            IncomeTaxAmount: 100,
            LuxuryTaxAmount: 100,
            BaseRentEnabled: true,
            UpgradesEnabled: false,
            MortgagesEnabled: true,
            MortgageValuePercent: 50,
            UnmortgageInterestPercent: 10,
            UpgradeSellRefundPercent: 50),
        Auction: new AuctionRules(
            MandatoryAuctionsEnabled: true,
            InitialTimerSeconds: 9,
            BidResetTimerSeconds: 3,
            MinimumBidIncrement: 1,
            StartingBid: 0),
        Jail: new JailRules(
            Enabled: true,
            EscapeCardsEnabled: true,
            FineAmount: 50,
            MaxTurns: 3),
        Dice: new DiceRules(
            DiceCount: 2,
            SidesPerDie: 6,
            DoublesExtraTurnEnabled: false,
            MaxConsecutiveDoublesBeforeLockup: 3,
            ResolveLandingAfterCardMove: false),
        Cards: new CardRules(
            new[] { CardDeckIds.Chance, CardDeckIds.Table },
            customCardsEnabled: true,
            deckEditingEnabled: true,
            deckPresetId: CardDeckPresetIds.Default),
        Loans: new LoanRules(
            LoanSharkEnabled: true,
            BaseInterestRate: 0.20m,
            InterestRateIncreasePerLoan: 0.10m,
            InterestRateIncreasePerDebtTier: 0.20m,
            MinimumInterestPayment: 0,
            CanBorrowForLoanPayments: false),
        Win: new WinRules(
            ConditionType: "lastPlayerStanding"),
        Future: new FutureRules(
            SlimerEnabled: false,
            EarthquakeEnabled: false));
}
