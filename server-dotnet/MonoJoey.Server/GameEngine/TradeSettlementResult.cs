namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record PropertyOwnershipChange(
    TileId PropertyTileId,
    PlayerId PreviousOwnerId,
    PlayerId NewOwnerId);

public sealed record TradeSettlementResult(
    TradeSettlementResultKind ResultKind,
    GameState GameState,
    PlayerId FirstPlayerId,
    PlayerId SecondPlayerId,
    TradeAssets FirstPlayerAssets,
    TradeAssets SecondPlayerAssets,
    IReadOnlyList<PlayerCashTransferResult> CashTransferResults,
    IReadOnlyList<PropertyOwnershipChange> OwnershipChanges,
    string Message)
{
    public bool TradeSettled => ResultKind == TradeSettlementResultKind.Settled;
}

public enum TradeSettlementResultKind
{
    Settled,
    GameNotInProgress,
    ActiveAuction,
    UnresolvedTileExecution,
    SamePlayer,
    PlayerNotInGame,
    PlayerBankrupt,
    PlayerEliminated,
    NegativeCash,
    EmptyTrade,
    DuplicateProperty,
    PropertyOfferedByBothSides,
    InvalidProperty,
    PropertyOwnedByMultiplePlayers,
    PropertyNotOwnedByOfferingPlayer,
    InsufficientCash,
    UnsafeMoneyBalance,
}
