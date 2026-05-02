namespace MonoJoey.Server.GameEngine;

public sealed record AuctionConfig(
    bool MandatoryAuctionsEnabled,
    int InitialPreBidSeconds,
    int BidResetSeconds,
    Money MinimumBidIncrement,
    Money StartingBid)
{
    public static AuctionConfig Default { get; } = FromRules(GameRulesPresets.MonoJoeyDefault.Auction);

    public static AuctionConfig FromRules(AuctionRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return new AuctionConfig(
            rules.MandatoryAuctionsEnabled,
            rules.InitialTimerSeconds,
            rules.BidResetTimerSeconds,
            new Money(rules.MinimumBidIncrement),
            new Money(rules.StartingBid));
    }
}
