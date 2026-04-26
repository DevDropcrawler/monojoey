namespace MonoJoey.Server.GameEngine;

public sealed record AuctionStartResult(
    AuctionStartResultKind ResultKind,
    AuctionState? AuctionState,
    string Message)
{
    public bool AuctionStarted => ResultKind == AuctionStartResultKind.Started && AuctionState is not null;
}

public enum AuctionStartResultKind
{
    Started = 0,
    MandatoryAuctionsDisabled,
    TileNotAuctionable,
    PropertyAlreadyOwned,
}
