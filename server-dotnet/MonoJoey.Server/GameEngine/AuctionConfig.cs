namespace MonoJoey.Server.GameEngine;

public sealed record AuctionConfig(
    bool MandatoryAuctionsEnabled,
    int InitialPreBidSeconds,
    int BidResetSeconds,
    Money MinimumBidIncrement,
    Money StartingBid)
{
    public static AuctionConfig Default { get; } = new(
        MandatoryAuctionsEnabled: true,
        InitialPreBidSeconds: 9,
        BidResetSeconds: 3,
        MinimumBidIncrement: new Money(1),
        StartingBid: Money.Zero);
}
