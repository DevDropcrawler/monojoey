namespace MonoJoey.Server.GameEngine;

public sealed record GameRules(
    int Version,
    string PresetId,
    string PresetName,
    bool IsCustom,
    EconomyRules Economy,
    AuctionRules Auction,
    JailRules Jail,
    DiceRules Dice,
    CardRules Cards,
    LoanRules Loans,
    WinRules Win,
    FutureRules Future)
{
    public GameRules DeepCopy()
    {
        return new GameRules(
            Version,
            PresetId,
            PresetName,
            IsCustom,
            Economy.DeepCopy(),
            Auction.DeepCopy(),
            Jail.DeepCopy(),
            Dice.DeepCopy(),
            Cards.DeepCopy(),
            Loans.DeepCopy(),
            Win.DeepCopy(),
            Future.DeepCopy());
    }
}

public sealed record EconomyRules(
    int StartingMoney,
    int PassStartReward,
    bool BaseRentEnabled,
    bool UpgradesEnabled)
{
    public EconomyRules DeepCopy()
    {
        return new EconomyRules(
            StartingMoney,
            PassStartReward,
            BaseRentEnabled,
            UpgradesEnabled);
    }
}

public sealed record AuctionRules(
    bool MandatoryAuctionsEnabled,
    int InitialTimerSeconds,
    int BidResetTimerSeconds,
    int MinimumBidIncrement,
    int StartingBid)
{
    public AuctionRules DeepCopy()
    {
        return new AuctionRules(
            MandatoryAuctionsEnabled,
            InitialTimerSeconds,
            BidResetTimerSeconds,
            MinimumBidIncrement,
            StartingBid);
    }
}

public sealed record JailRules(
    bool Enabled,
    bool EscapeCardsEnabled)
{
    public JailRules DeepCopy()
    {
        return new JailRules(
            Enabled,
            EscapeCardsEnabled);
    }
}

public sealed record DiceRules(
    int DiceCount,
    int SidesPerDie,
    bool ResolveLandingAfterCardMove)
{
    public DiceRules DeepCopy()
    {
        return new DiceRules(
            DiceCount,
            SidesPerDie,
            ResolveLandingAfterCardMove);
    }
}

public sealed record CardRules
{
    private readonly string[] decksEnabled;

    public CardRules(
        IEnumerable<string> decksEnabled,
        bool customCardsEnabled,
        bool deckEditingEnabled)
    {
        this.decksEnabled = decksEnabled.ToArray();
        CustomCardsEnabled = customCardsEnabled;
        DeckEditingEnabled = deckEditingEnabled;
    }

    public IReadOnlyList<string> DecksEnabled => decksEnabled.ToArray();

    public bool CustomCardsEnabled { get; }

    public bool DeckEditingEnabled { get; }

    public CardRules DeepCopy()
    {
        return new CardRules(
            decksEnabled,
            CustomCardsEnabled,
            DeckEditingEnabled);
    }
}

public sealed record LoanRules(
    bool LoanSharkEnabled,
    decimal BaseInterestRate,
    decimal InterestRateIncreasePerLoan,
    decimal InterestRateIncreasePerDebtTier,
    int MinimumInterestPayment,
    bool CanBorrowForLoanPayments)
{
    public LoanRules DeepCopy()
    {
        return new LoanRules(
            LoanSharkEnabled,
            BaseInterestRate,
            InterestRateIncreasePerLoan,
            InterestRateIncreasePerDebtTier,
            MinimumInterestPayment,
            CanBorrowForLoanPayments);
    }
}

public sealed record WinRules(
    string ConditionType)
{
    public WinRules DeepCopy()
    {
        return new WinRules(ConditionType);
    }
}

public sealed record FutureRules(
    bool SlimerEnabled,
    bool EarthquakeEnabled)
{
    public FutureRules DeepCopy()
    {
        return new FutureRules(
            SlimerEnabled,
            EarthquakeEnabled);
    }
}
