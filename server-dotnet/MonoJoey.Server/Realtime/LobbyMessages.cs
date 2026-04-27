namespace MonoJoey.Server.Realtime;

public static class LobbyMessageTypes
{
    public const string CreateLobby = "create_lobby";
    public const string JoinLobby = "join_lobby";
    public const string LeaveLobby = "leave_lobby";
    public const string LobbyState = "lobby_state";
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
