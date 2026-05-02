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
            UpgradesEnabled: false),
        Auction: new AuctionRules(
            MandatoryAuctionsEnabled: true,
            InitialTimerSeconds: 9,
            BidResetTimerSeconds: 3,
            MinimumBidIncrement: 1,
            StartingBid: 0),
        Jail: new JailRules(
            Enabled: true,
            EscapeCardsEnabled: true),
        Dice: new DiceRules(
            DiceCount: 2,
            SidesPerDie: 6,
            ResolveLandingAfterCardMove: false),
        Cards: new CardRules(
            new[] { CardDeckIds.Chance, CardDeckIds.Table },
            customCardsEnabled: true,
            deckEditingEnabled: true),
        Loans: new LoanRules(
            LoanSharkEnabled: true,
            BaseInterestRate: 0.25m,
            InterestRateIncreasePerLoan: 0.10m,
            InterestRateIncreasePerDebtTier: 0.05m,
            MinimumInterestPayment: 25,
            CanBorrowForLoanPayments: false),
        Win: new WinRules(
            ConditionType: "lastPlayerStanding"),
        Future: new FutureRules(
            SlimerEnabled: false,
            EarthquakeEnabled: false));
}
