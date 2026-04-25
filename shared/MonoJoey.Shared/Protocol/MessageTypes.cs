namespace MonoJoey.Shared.Protocol;

public enum ClientMessageType
{
    CreateLobbyRequest,
    JoinLobbyRequest,
    LeaveLobbyRequest,
    SetPlayerProfileRequest,
    SetReadyRequest,
    StartMatchRequest,
    RollDiceRequest,
    PlaceBidRequest,
    TakeLoanRequest,
    UseHeldCardRequest,
    EndTurnRequest,
    RequestSnapshot,
    TradeOfferRequest,
    MortgagePropertyRequest,
    UpgradePropertyRequest,
    ChatMessageRequest
}

public enum ServerEventType
{
    LobbyCreated,
    LobbyJoined,
    LobbyPlayerUpdated,
    LobbyPlayerReadyChanged,
    LobbyStartRejected,
    MatchStarted,
    TurnStarted,
    LoanInterestCharged,
    PlayerEliminated,
    DiceRolled,
    PlayerMoved,
    TileResolved,
    AuctionStarted,
    BidAccepted,
    BidRejected,
    AuctionTimerReset,
    AuctionEndedNoSale,
    AuctionWon,
    PropertyTransferred,
    MoneyChanged,
    CardDrawn,
    CardResolved,
    TurnEnded,
    MatchCompleted,
    SnapshotProvided,
    ErrorEvent
}

public enum ErrorCode
{
    NotCurrentPlayer,
    InvalidPhase,
    AuctionNotActive,
    BidTooLow,
    TokenAlreadyTaken,
    ColorAlreadyTaken,
    LoanReasonBlocked,
    InsufficientFunds,
    MatchNotFound
}
