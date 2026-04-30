namespace MonoJoey.Server.Realtime;

public static class LobbyMessageTypes
{
    public const string CreateLobby = "create_lobby";
    public const string JoinLobby = "join_lobby";
    public const string LeaveLobby = "leave_lobby";
    public const string SetReady = "set_ready";
    public const string StartGame = "start_game";
    public const string RollDice = "roll_dice";
    public const string ResolveTile = "resolve_tile";
    public const string ExecuteTile = "execute_tile";
    public const string EndTurn = "end_turn";
    public const string PlaceBid = "place_bid";
    public const string LobbyState = "lobby_state";
    public const string GameStarted = "game_started";
    public const string RollResult = "roll_result";
    public const string ResolveTileResult = "resolve_tile_result";
    public const string ExecuteTileResult = "execute_tile_result";
    public const string EndTurnResult = "end_turn_result";
    public const string BidResult = "bid_result";
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
}

public sealed record LobbyServerEnvelope(
    string Type,
    object Payload);

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
    bool IsReady);

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
    ExecuteTileRentPayload? Rent);

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
    int? CountdownDurationSeconds);

public sealed record ExecuteTileRentPayload(
    string PayerId,
    string? OwnerId,
    int RentDue,
    int RentPaid,
    int PayerMoney,
    int? OwnerMoney,
    bool PlayerEliminated,
    string? EliminationReason);

public sealed record EndTurnResultPayload(
    string PreviousPlayerId,
    string? NextPlayerId,
    int TurnIndex);

public sealed record BidResultPayload(
    string BidderPlayerId,
    int Amount,
    int CurrentHighestBid,
    string HighestBidderId);
