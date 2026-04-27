namespace MonoJoey.Server.Realtime;

public static class LobbyMessageTypes
{
    public const string CreateLobby = "create_lobby";
    public const string JoinLobby = "join_lobby";
    public const string LeaveLobby = "leave_lobby";
    public const string SetReady = "set_ready";
    public const string StartGame = "start_game";
    public const string LobbyState = "lobby_state";
    public const string GameStarted = "game_started";
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
