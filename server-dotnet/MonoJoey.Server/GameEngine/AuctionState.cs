namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record AuctionState(
    TileId PropertyTileId,
    PlayerId TriggeringPlayerId,
    AuctionStatus Status,
    Money StartingBid,
    Money MinimumBidIncrement,
    int InitialPreBidSeconds,
    int BidResetSeconds,
    IReadOnlyList<AuctionBid> Bids,
    Money? HighestBid,
    PlayerId? HighestBidderId,
    int? CountdownDurationSeconds);
