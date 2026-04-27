namespace MonoJoey.Server.Realtime;

using System.Text.Json;
using MonoJoey.Server.Sessions;
using MonoJoey.Shared.Protocol;

public sealed class LobbyMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object sessionLock = new();
    private readonly SessionManager sessionManager;

    public LobbyMessageHandler(SessionManager sessionManager)
    {
        this.sessionManager = sessionManager;
    }

    public string HandleTextMessage(string messageJson, LobbyConnectionContext connectionContext)
    {
        ArgumentNullException.ThrowIfNull(messageJson);
        ArgumentNullException.ThrowIfNull(connectionContext);

        var envelope = HandleParsedMessage(messageJson, connectionContext);

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    public string CreateErrorMessage(string code, string message)
    {
        return JsonSerializer.Serialize(CreateError(code, message), JsonOptions);
    }

    public void CleanupConnection(LobbyConnectionContext connectionContext)
    {
        ArgumentNullException.ThrowIfNull(connectionContext);

        if (!connectionContext.IsBound)
        {
            return;
        }

        lock (sessionLock)
        {
            if (connectionContext.SessionId is null || connectionContext.PlayerId is null)
            {
                return;
            }

            var session = sessionManager.GetSession(connectionContext.SessionId);
            if (session is not null)
            {
                _ = sessionManager.LeaveSession(
                    connectionContext.SessionId,
                    new PlayerConnection(
                        new PlayerId(connectionContext.PlayerId),
                        connectionContext.ConnectionId,
                        IsReady: false));
            }

            connectionContext.ClearBinding();
        }
    }

    private LobbyServerEnvelope HandleParsedMessage(
        string messageJson,
        LobbyConnectionContext connectionContext)
    {
        using var document = ParseMessage(messageJson);
        if (document is null)
        {
            return CreateError(
                LobbyErrorCodes.InvalidMessage,
                "Message must be valid JSON.");
        }

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !TryReadString(root, "type", out var type))
        {
            return CreateError(
                LobbyErrorCodes.InvalidMessage,
                "Message must include a type.");
        }

        return type switch
        {
            LobbyMessageTypes.CreateLobby => HandleCreateLobby(),
            LobbyMessageTypes.JoinLobby => HandleJoinLobby(root, connectionContext),
            LobbyMessageTypes.LeaveLobby => HandleLeaveLobby(root, connectionContext),
            LobbyMessageTypes.LobbyState or LobbyMessageTypes.Error => CreateError(
                LobbyErrorCodes.UnsupportedMessage,
                "This message type is not supported from clients."),
            _ => CreateError(
                LobbyErrorCodes.UnknownMessageType,
                "Message type is not recognized."),
        };
    }

    private LobbyServerEnvelope HandleCreateLobby()
    {
        lock (sessionLock)
        {
            var session = sessionManager.CreateSession();

            return CreateLobbyState(session);
        }
    }

    private LobbyServerEnvelope HandleJoinLobby(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadLobbyPlayerPayload(root, out var sessionId, out var playerId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "join_lobby requires payload.sessionId and payload.playerId.");
        }

        if (connectionContext.IsBoundToDifferentPlayer(playerId))
        {
            return CreateError(
                LobbyErrorCodes.PlayerSwitchRejected,
                "This connection is already bound to a different playerId.");
        }

        lock (sessionLock)
        {
            try
            {
                var updatedSession = sessionManager.JoinSession(
                    sessionId,
                    new PlayerConnection(
                        new PlayerId(playerId),
                        connectionContext.ConnectionId,
                        IsReady: false));

                RemovePreviousSessionBindingIfNeeded(connectionContext, sessionId, playerId);
                connectionContext.Bind(sessionId, playerId);

                return CreateLobbyState(updatedSession);
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "Session not found.")
            {
                return CreateError(
                    LobbyErrorCodes.SessionNotFound,
                    "Session not found.");
            }
        }
    }

    private LobbyServerEnvelope HandleLeaveLobby(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadLobbyPlayerPayload(root, out var sessionId, out var playerId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "leave_lobby requires payload.sessionId and payload.playerId.");
        }

        lock (sessionLock)
        {
            var session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return CreateError(
                    LobbyErrorCodes.SessionNotFound,
                    "Session not found.");
            }

            if (!string.Equals(connectionContext.PlayerId, playerId, StringComparison.Ordinal))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerSwitchRejected,
                    "This connection is not bound to that playerId.");
            }

            var updatedSession = sessionManager.LeaveSession(
                sessionId,
                new PlayerConnection(
                    new PlayerId(playerId),
                    connectionContext.ConnectionId,
                    IsReady: false));

            if (string.Equals(connectionContext.SessionId, sessionId, StringComparison.Ordinal))
            {
                connectionContext.ClearBinding();
            }

            return CreateLobbyState(updatedSession);
        }
    }

    private void RemovePreviousSessionBindingIfNeeded(
        LobbyConnectionContext connectionContext,
        string sessionId,
        string playerId)
    {
        if (connectionContext.SessionId is null ||
            string.Equals(connectionContext.SessionId, sessionId, StringComparison.Ordinal))
        {
            return;
        }

        var previousSession = sessionManager.GetSession(connectionContext.SessionId);
        if (previousSession is null)
        {
            return;
        }

        _ = sessionManager.LeaveSession(
            connectionContext.SessionId,
            new PlayerConnection(
                new PlayerId(playerId),
                connectionContext.ConnectionId,
                IsReady: false));
    }

    private static JsonDocument? ParseMessage(string messageJson)
    {
        try
        {
            return JsonDocument.Parse(messageJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadLobbyPlayerPayload(
        JsonElement root,
        out string sessionId,
        out string playerId)
    {
        sessionId = string.Empty;
        playerId = string.Empty;

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !TryReadString(payload, "sessionId", out sessionId) ||
            !TryReadString(payload, "playerId", out playerId))
        {
            return false;
        }

        return true;
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var rawValue = property.GetString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        value = rawValue;
        return true;
    }

    private static LobbyServerEnvelope CreateLobbyState(GameSession session)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.LobbyState,
            new LobbyStatePayload(
                session.SessionId,
                session.Status.ToString().ToLowerInvariant(),
                session.Players
                    .Select(player => new LobbyPlayerPayload(
                        player.PlayerId.Value,
                        player.ConnectionId,
                        player.IsReady))
                    .ToArray()));
    }

    private static LobbyServerEnvelope CreateError(string code, string message)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.Error,
            new LobbyErrorPayload(code, message));
    }
}
