namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record AuctionFinalizationResult(
    AuctionFinalizationResultKind ResultKind,
    GameState GameState,
    AuctionState AuctionState,
    TileId PropertyTileId,
    PlayerId? WinnerId,
    Money? WinningBid,
    PlayerEliminationResult? EliminationResult,
    string Message)
{
    public bool FinalizedWithWinner => ResultKind == AuctionFinalizationResultKind.FinalizedWithWinner;

    public bool FinalizedWithoutWinner => ResultKind == AuctionFinalizationResultKind.FinalizedNoWinner;

    public bool WinnerFailedToPay => ResultKind == AuctionFinalizationResultKind.WinnerFailedToPay;
}

public enum AuctionFinalizationResultKind
{
    FinalizedNoWinner = 0,
    FinalizedWithWinner,
    WinnerFailedToPay,
    InvalidAuctionState,
}
