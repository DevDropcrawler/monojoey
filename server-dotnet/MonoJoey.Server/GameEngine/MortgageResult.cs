namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record MortgageResult(
    MortgageResultKind ResultKind,
    GameState GameState,
    PlayerId PlayerId,
    TileId PropertyTileId,
    Money MortgageValue,
    Money Money,
    bool IsMortgaged,
    string Message)
{
    public bool MortgageAccepted => ResultKind == MortgageResultKind.Mortgaged;
}

public enum MortgageResultKind
{
    Mortgaged,
    MortgagesDisabled,
    PlayerNotInGame,
    PlayerEliminated,
    GameNotInProgress,
    ActiveAuction,
    UnresolvedTileExecution,
    InvalidProperty,
    PropertyNotOwned,
    AlreadyMortgaged,
    UnsafeMoneyBalance,
}

public sealed record UnmortgageResult(
    UnmortgageResultKind ResultKind,
    GameState GameState,
    PlayerId PlayerId,
    TileId PropertyTileId,
    Money MortgageValue,
    Money UnmortgageInterest,
    Money UnmortgageCost,
    Money Money,
    bool IsMortgaged,
    string Message)
{
    public bool UnmortgageAccepted => ResultKind == UnmortgageResultKind.Unmortgaged;
}

public enum UnmortgageResultKind
{
    Unmortgaged,
    MortgagesDisabled,
    PlayerNotInGame,
    PlayerEliminated,
    GameNotInProgress,
    ActiveAuction,
    UnresolvedTileExecution,
    InvalidProperty,
    PropertyNotOwned,
    NotMortgaged,
    InsufficientCash,
    UnsafeMoneyBalance,
}
