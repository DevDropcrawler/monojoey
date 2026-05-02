namespace MonoJoey.Server.Realtime;

public static class LobbyMessageTypes
{
    public const string CreateLobby = "create_lobby";
    public const string JoinLobby = "join_lobby";
    public const string LeaveLobby = "leave_lobby";
    public const string SetProfile = "set_profile";
    public const string SetReady = "set_ready";
    public const string StartGame = "start_game";
    public const string RollDice = "roll_dice";
    public const string ResolveTile = "resolve_tile";
    public const string ExecuteTile = "execute_tile";
    public const string EndTurn = "end_turn";
    public const string PlaceBid = "place_bid";
    public const string FinalizeAuction = "finalize_auction";
    public const string TakeLoan = "take_loan";
    public const string UseHeldCard = "use_held_card";
    public const string GetSnapshot = "get_snapshot";
    public const string ReconnectSession = "reconnect_session";
    public const string LobbyState = "lobby_state";
    public const string GameStarted = "game_started";
    public const string RollResult = "roll_result";
    public const string ResolveTileResult = "resolve_tile_result";
    public const string ExecuteTileResult = "execute_tile_result";
    public const string EndTurnResult = "end_turn_result";
    public const string BidResult = "bid_result";
    public const string AuctionResult = "auction_result";
    public const string LoanResult = "loan_result";
    public const string UseHeldCardResult = "use_held_card_result";
    public const string SnapshotResult = "snapshot_result";
    public const string ReconnectResult = "reconnect_result";
    public const string DiceRolled = "dice_rolled";
    public const string TileResolved = "tile_resolved";
    public const string TileExecuted = "tile_executed";
    public const string TurnEnded = "turn_ended";
    public const string BidAccepted = "bid_accepted";
    public const string AuctionFinalized = "auction_finalized";
    public const string LoanTaken = "loan_taken";
    public const string HeldCardUsed = "held_card_used";
    public const string GameCompleted = "game_completed";
    public const string Error = "error";
}

public static class LobbyErrorCodes
{
    public const string InvalidMessage = "invalid_message";
    public const string UnknownMessageType = "unknown_message_type";
    public const string InvalidPayload = "invalid_payload";
    public const string SessionNotFound = "session_not_found";
    public const string PlayerSwitchRejected = "player_switch_rejected";
    public const string UnsupportedMessage = "unsupported_message";
    public const string PlayerNotInLobby = "player_not_in_lobby";
    public const string InvalidSessionStatus = "invalid_session_status";
    public const string UsernameTaken = "username_taken";
    public const string TokenTaken = "token_taken";
    public const string ColorTaken = "color_taken";
    public const string NotEnoughPlayers = "not_enough_players";
    public const string PlayersNotReady = "players_not_ready";
    public const string InvalidSession = "invalid_session";
    public const string InvalidSessionState = "invalid_session_state";
    public const string NotYourTurn = "not_your_turn";
    public const string PlayerNotFound = "player_not_found";
    public const string PlayerEliminated = "player_eliminated";
    public const string PlayerLocked = "player_locked";
    public const string UnsupportedTileEffect = "unsupported_tile_effect";
    public const string AuctionNotActive = "auction_not_active";
    public const string BidTooLow = "bid_too_low";
    public const string InvalidLoanAmount = "invalid_loan_amount";
    public const string LoanModeDisabled = "loan_mode_disabled";
    public const string LoanReasonBlocked = "loan_reason_blocked";
    public const string CardDeckNotFound = "card_deck_not_found";
    public const string CardDeckEmpty = "card_deck_empty";
    public const string InvalidCard = "invalid_card";
    public const string UnsupportedCardAction = "unsupported_card_action";
    public const string GameAlreadyCompleted = "game_already_completed";
    public const string HeldCardNotHeld = "held_card_not_held";
}

public sealed record LobbyServerEnvelope(
    string Type,
    object Payload);

public sealed record LobbyBroadcastEnvelope(
    string Type,
    long Sequence,
    string SessionId,
    string MatchId,
    DateTimeOffset CreatedAtUtc,
    object Payload);

public sealed record LobbyMessageHandleResult
{
    public LobbyMessageHandleResult(
        LobbyServerEnvelope directResponse,
        LobbyBroadcastEnvelope? broadcast,
        IReadOnlyList<string> broadcastConnectionIds)
        : this(
            directResponse,
            broadcast is null
                ? Array.Empty<LobbyBroadcastEnvelope>()
                : new[] { broadcast },
            broadcastConnectionIds)
    {
    }

    public LobbyMessageHandleResult(
        LobbyServerEnvelope directResponse,
        IReadOnlyList<LobbyBroadcastEnvelope> broadcasts,
        IReadOnlyList<string> broadcastConnectionIds)
    {
        DirectResponse = directResponse;
        Broadcasts = broadcasts;
        BroadcastConnectionIds = broadcastConnectionIds;
    }

    public LobbyServerEnvelope DirectResponse { get; }

    public LobbyBroadcastEnvelope? Broadcast => Broadcasts.Count == 0 ? null : Broadcasts[0];

    public IReadOnlyList<LobbyBroadcastEnvelope> Broadcasts { get; }

    public IReadOnlyList<string> BroadcastConnectionIds { get; }

    public static implicit operator LobbyMessageHandleResult(LobbyServerEnvelope directResponse)
    {
        return new LobbyMessageHandleResult(
            directResponse,
            broadcast: null,
            broadcastConnectionIds: Array.Empty<string>());
    }
}

public sealed record LobbyErrorPayload(
    string Code,
    string Message);

public sealed record LobbyStatePayload(
    string SessionId,
    string Status,
    IReadOnlyList<LobbyPlayerPayload> Players);

public sealed record LobbyPlayerPayload(
    string PlayerId,
    string ConnectionId,
    bool IsReady,
    string Username,
    string TokenId,
    string ColorId);

public sealed record GameStartedPayload(
    string SessionId,
    string Status,
    string Phase,
    string? CurrentTurnPlayerId,
    IReadOnlyList<GameStartedPlayerPayload> Players);

public sealed record GameStartedPlayerPayload(
    string PlayerId,
    string Username,
    string TokenId,
    string ColorId,
    string CurrentTileId,
    int Money);

public sealed record RollResultPayload(
    string PlayerId,
    IReadOnlyList<int> Dice,
    string NewPosition,
    bool PassedStart,
    bool HasRolledThisTurn);

public sealed record ResolveTileResultPayload(
    string PlayerId,
    string TileId,
    int TileIndex,
    string TileType,
    bool RequiresAction,
    string ActionKind);

public sealed record ExecuteTileResultPayload(
    string PlayerId,
    string TileId,
    int TileIndex,
    string TileType,
    string ActionKind,
    string ExecutionKind,
    string Phase,
    bool HasExecutedTileThisTurn,
    ExecuteTileAuctionPayload? Auction,
    ExecuteTileRentPayload? Rent,
    ExecuteTileCardPayload? Card);

public sealed record ExecuteTileAuctionPayload(
    string PropertyTileId,
    string TriggeringPlayerId,
    string Status,
    int StartingBid,
    int MinimumBidIncrement,
    int InitialPreBidSeconds,
    int BidResetSeconds,
    int? HighestBid,
    string? HighestBidderId,
    int? CountdownDurationSeconds,
    DateTimeOffset? TimerEndsAtUtc);

public sealed record ExecuteTileRentPayload(
    string PayerId,
    string? OwnerId,
    int RentDue,
    int RentPaid,
    int PayerMoney,
    int? OwnerMoney,
    bool PlayerEliminated,
    string? EliminationReason);

public sealed record ExecuteTileCardPayload(
    string DeckId,
    string CardId,
    string DisplayName,
    string ResolutionKind,
    string ExecutionKind,
    string PlayerId,
    string CurrentTileId,
    int Money,
    bool IsEliminated,
    bool IsLockedUp,
    IReadOnlyList<string> HeldCardIds);

public sealed record EndTurnResultPayload(
    string PreviousPlayerId,
    string? NextPlayerId,
    int TurnIndex);

public sealed record BidResultPayload(
    string BidderPlayerId,
    int Amount,
    int CurrentHighestBid,
    string HighestBidderId,
    DateTimeOffset? TimerEndsAtUtc);

public sealed record AuctionResultPayload(
    string ResultType,
    string? WinnerPlayerId,
    int Amount,
    string TileId);

public sealed record LoanResultPayload(
    string PlayerId,
    int Amount,
    string Reason,
    int Money,
    int TotalBorrowed,
    int CurrentInterestRatePercent,
    int NextTurnInterestDue,
    int LoanTier);

public sealed record UseHeldCardResultPayload(
    string PlayerId,
    string CardId,
    bool IsLockedUp,
    IReadOnlyList<string> HeldCardIds);

public sealed record GameCompletedPayload(
    string WinnerPlayerId,
    int TurnIndex,
    DateTimeOffset EndedAtUtc,
    int ActivePlayerCount,
    IReadOnlyList<string> EliminatedPlayerIds);

public sealed record SnapshotPayload(
    int SnapshotVersion,
    string SessionId,
    string Status,
    string GameStatus,
    DateTimeOffset ServerNowUtc,
    string MatchId,
    string Phase,
    string? WinnerPlayerId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    SnapshotTurnPayload Turn,
    IReadOnlyList<SnapshotPlayerPayload> Players,
    SnapshotBoardPayload Board,
    SnapshotAuctionPayload? ActiveAuction,
    IReadOnlyList<SnapshotCardDeckPayload> CardDecks,
    SnapshotLoanSharkPayload LoanShark);

public sealed record ReconnectResultPayload(
    string SessionId,
    string PlayerId,
    long LastEventSequence,
    SnapshotPayload Snapshot);

public sealed record SnapshotTurnPayload(
    string? CurrentPlayerId,
    int TurnIndex,
    bool HasRolledThisTurn,
    bool HasResolvedTileThisTurn,
    bool HasExecutedTileThisTurn);

public sealed record SnapshotPlayerPayload(
    string PlayerId,
    string Username,
    string TokenId,
    string ColorId,
    int Money,
    string CurrentTileId,
    IReadOnlyList<string> OwnedPropertyIds,
    IReadOnlyList<string> HeldCardIds,
    SnapshotPlayerLoanPayload Loan,
    bool IsBankrupt,
    bool IsEliminated,
    bool IsLockedUp);

public sealed record SnapshotPlayerLoanPayload(
    int TotalBorrowed,
    int CurrentInterestRatePercent,
    int NextTurnInterestDue,
    int LoanTier);

public sealed record SnapshotBoardPayload(
    string BoardId,
    int Version,
    string DisplayName,
    IReadOnlyList<SnapshotBoardTilePayload> Tiles);

public sealed record SnapshotBoardTilePayload(
    string TileId,
    int Index,
    string DisplayName,
    string TileType,
    string? GroupId,
    int? Price,
    IReadOnlyList<int> RentTable,
    int? UpgradeCost,
    bool IsPurchasable,
    bool IsAuctionable,
    string? OwnerPlayerId);

public sealed record SnapshotAuctionPayload(
    string PropertyTileId,
    string TriggeringPlayerId,
    string Status,
    int StartingBid,
    int MinimumBidIncrement,
    int InitialPreBidSeconds,
    int BidResetSeconds,
    int? HighestBid,
    string? HighestBidderId,
    int? CountdownDurationSeconds,
    DateTimeOffset? TimerEndsAtUtc,
    IReadOnlyList<SnapshotAuctionBidPayload> Bids);

public sealed record SnapshotAuctionBidPayload(
    string BidderPlayerId,
    int Amount,
    DateTimeOffset PlacedAtUtc);

public sealed record SnapshotCardDeckPayload(
    string DeckId,
    IReadOnlyList<string> DrawPileCardIds,
    IReadOnlyList<string> DiscardPileCardIds);

public sealed record SnapshotLoanSharkPayload(
    bool Enabled);
