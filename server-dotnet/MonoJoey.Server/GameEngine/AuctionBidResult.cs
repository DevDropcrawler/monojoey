namespace MonoJoey.Server.GameEngine;

public sealed record AuctionBidResult(
    AuctionBidResultKind ResultKind,
    AuctionState AuctionState,
    string Message)
{
    public bool BidAccepted => ResultKind == AuctionBidResultKind.Accepted;
}

public enum AuctionBidResultKind
{
    Accepted = 0,
    BidderNotInGame,
    BidderEliminated,
    BidBelowStartingBid,
    BidBelowMinimumIncrement,
}
