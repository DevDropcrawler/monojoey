namespace MonoJoey.Server.Tests.Realtime;

using System.Text.Json;
using MonoJoey.Server.Realtime;
using MonoJoey.Server.Sessions;

public class LobbyMessageHandlerTests
{
    [Fact]
    public void CreateLobby_ReturnsLobbyState()
    {
        var sessionManager = new SessionManager();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, @"{""type"":""create_lobby""}");
        var payload = AssertResponseType(response, "lobby_state");

        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("sessionId").GetString()));
        Assert.Equal("lobby", payload.GetProperty("status").GetString());
        Assert.Empty(payload.GetProperty("players").EnumerateArray());
    }

    [Fact]
    public void JoinLobby_AddsPlayerAndReturnsLobbyState()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(
            handler,
            context,
            JoinMessage(session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "lobby_state");
        var player = Assert.Single(payload.GetProperty("players").EnumerateArray());

        Assert.Equal(session.SessionId, payload.GetProperty("sessionId").GetString());
        Assert.Equal("player_1", player.GetProperty("playerId").GetString());
        Assert.Equal("connection_1", player.GetProperty("connectionId").GetString());
        Assert.False(player.GetProperty("isReady").GetBoolean());
        Assert.Single(sessionManager.GetSession(session.SessionId)?.Players ?? Array.Empty<PlayerConnection>());
    }

    [Fact]
    public void JoinLobby_DuplicateJoinIsIdempotent()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");

        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);
        using var response = Handle(handler, context, JoinMessage(session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "lobby_state");

        Assert.Single(payload.GetProperty("players").EnumerateArray());
        Assert.Single(sessionManager.GetSession(session.SessionId)?.Players ?? Array.Empty<PlayerConnection>());
    }

    [Fact]
    public void LeaveLobby_RemovesPlayerAndReturnsLobbyState()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(
            handler,
            context,
            LeaveMessage(session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "lobby_state");

        Assert.Empty(payload.GetProperty("players").EnumerateArray());
        Assert.Empty(sessionManager.GetSession(session.SessionId)?.Players ?? Array.Empty<PlayerConnection>());
    }

    [Theory]
    [InlineData(@"{""type"":""join_lobby"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""join_lobby"",""payload"":{""sessionId"":""session_1""}}")]
    [InlineData(@"{""type"":""leave_lobby"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""leave_lobby"",""payload"":{""sessionId"":""session_1""}}")]
    public void MissingSessionIdOrPlayerId_ReturnsInvalidPayload(string message)
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, message);

        AssertError(response, "invalid_payload");
    }

    [Fact]
    public void MissingSession_ReturnsSessionNotFound()
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, JoinMessage("missing_session", "player_1"));

        AssertError(response, "session_not_found");
    }

    [Fact]
    public void UnknownMessageType_ReturnsUnknownMessageType()
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, @"{""type"":""dance""}");

        AssertError(response, "unknown_message_type");
    }

    [Fact]
    public void InvalidJson_ReturnsInvalidMessage()
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, "{");

        AssertError(response, "invalid_message");
    }

    [Fact]
    public void SameConnectionCannotSwitchToDifferentPlayerId()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(handler, context, JoinMessage(session.SessionId, "player_2"));

        AssertError(response, "player_switch_rejected");
        var player = Assert.Single(sessionManager.GetSession(session.SessionId)?.Players ?? Array.Empty<PlayerConnection>());
        Assert.Equal("player_1", player.PlayerId.Value);
    }

    private static JsonDocument Handle(
        LobbyMessageHandler handler,
        LobbyConnectionContext context,
        string message)
    {
        return JsonDocument.Parse(handler.HandleTextMessage(message, context));
    }

    private static JsonElement AssertResponseType(JsonDocument response, string type)
    {
        var root = response.RootElement;

        Assert.Equal(type, root.GetProperty("type").GetString());
        Assert.True(root.TryGetProperty("payload", out var payload));

        return payload;
    }

    private static void AssertError(JsonDocument response, string code)
    {
        var payload = AssertResponseType(response, "error");

        Assert.Equal(code, payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("message").GetString()));
    }

    private static string JoinMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""join_lobby"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string LeaveMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""leave_lobby"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }
}
