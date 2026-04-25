namespace MonoJoey.Shared.Schemas;

public enum GamePhase
{
    Lobby,
    AwaitingRoll,
    ResolvingTurn,
    Auction,
    AwaitingEndTurn,
    Completed
}

public enum TileType
{
    Start,
    Property,
    Transport,
    Utility,
    ChanceDeck,
    TableDeck,
    Tax,
    Lockup,
    GoToLockup,
    FreeSpace
}

public enum CardActionType
{
    MoveToTile,
    MoveRelative,
    MoveToNearestType,
    ReceiveFromBank,
    PayBank,
    PayEachOpponent,
    CollectFromEachOpponent,
    PayPerUpgrade,
    HoldableCancelStatus
}

public enum MoneyReason
{
    StartingMoney,
    PassStartReward,
    RentPayment,
    TaxPayment,
    AuctionBid,
    AuctionRefund,
    CardReward,
    CardPenalty,
    LoanPrincipalReceived,
    LoanInterestPayment,
    LoanPrincipalRepayment
}
