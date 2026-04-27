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

    [Fact]
    public void SetReady_ReturnsLobbyStateWithUpdatedReadyState()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(handler, context, SetReadyMessage(session.SessionId, "player_1", isReady: true));
        var payload = AssertResponseType(response, "lobby_state");
        var player = Assert.Single(payload.GetProperty("players").EnumerateArray());

        Assert.True(player.GetProperty("isReady").GetBoolean());
        Assert.True(sessionManager.GetSession(session.SessionId)?.Players[0].IsReady);
    }

    [Theory]
    [InlineData(@"{""type"":""set_ready"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""set_ready"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""isReady"":""true""}}")]
    public void SetReady_MissingOrNonBooleanIsReadyReturnsInvalidPayload(string message)
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, message);

        AssertError(response, "invalid_payload");
    }

    [Fact]
    public void SetReady_UnboundContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, SetReadyMessage(session.SessionId, "player_1", isReady: true));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void SetReady_WrongPlayerContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(handler, context, SetReadyMessage(session.SessionId, "player_2", isReady: true));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void SetReady_WrongSessionContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var firstSession = sessionManager.CreateSession();
        var secondSession = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(firstSession.SessionId, "player_1"), context);

        using var response = Handle(handler, context, SetReadyMessage(secondSession.SessionId, "player_1", isReady: true));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void SetReady_AfterGameStartReturnsInvalidSessionStatus()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var firstContext = new LobbyConnectionContext("connection_1");
        var secondContext = new LobbyConnectionContext("connection_2");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), firstContext);
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_2"), secondContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_1", isReady: true), firstContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_2", isReady: true), secondContext);
        _ = handler.HandleTextMessage(StartGameMessage(session.SessionId, "player_1"), firstContext);

        using var response = Handle(handler, firstContext, SetReadyMessage(session.SessionId, "player_1", isReady: false));

        AssertError(response, "invalid_session_status");
    }

    [Fact]
    public void StartGame_ReturnsGameStartedForReadyLobby()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var firstContext = new LobbyConnectionContext("connection_1");
        var secondContext = new LobbyConnectionContext("connection_2");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), firstContext);
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_2"), secondContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_1", isReady: true), firstContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_2", isReady: true), secondContext);

        using var response = Handle(handler, firstContext, StartGameMessage(session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "game_started");
        var players = payload.GetProperty("players").EnumerateArray().ToArray();

        Assert.Equal(session.SessionId, payload.GetProperty("sessionId").GetString());
        Assert.Equal("in_game", payload.GetProperty("status").GetString());
        Assert.Equal("awaiting_roll", payload.GetProperty("phase").GetString());
        Assert.Equal("player_1", payload.GetProperty("currentTurnPlayerId").GetString());
        Assert.Equal(2, players.Length);
        AssertGameStartedPlayer(players[0], "player_1");
        AssertGameStartedPlayer(players[1], "player_2");
        Assert.Equal(GameSessionStatus.InGame, sessionManager.GetSession(session.SessionId)?.Status);
    }

    [Fact]
    public void StartGame_ReturnsNotEnoughPlayersForOnePlayerLobby()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_1", isReady: true), context);

        using var response = Handle(handler, context, StartGameMessage(session.SessionId, "player_1"));

        AssertError(response, "not_enough_players");
    }

    [Fact]
    public void StartGame_ReturnsPlayersNotReadyWhenAnyPlayerIsNotReady()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var firstContext = new LobbyConnectionContext("connection_1");
        var secondContext = new LobbyConnectionContext("connection_2");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), firstContext);
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_2"), secondContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_1", isReady: true), firstContext);

        using var response = Handle(handler, firstContext, StartGameMessage(session.SessionId, "player_1"));

        AssertError(response, "players_not_ready");
    }

    [Fact]
    public void StartGame_UnboundContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, StartGameMessage(session.SessionId, "player_1"));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void StartGame_WrongPlayerContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(handler, context, StartGameMessage(session.SessionId, "player_2"));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void StartGame_WrongSessionContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var firstSession = sessionManager.CreateSession();
        var secondSession = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(firstSession.SessionId, "player_1"), context);

        using var response = Handle(handler, context, StartGameMessage(secondSession.SessionId, "player_1"));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void ClientSentGameStartedReturnsUnsupportedMessage()
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, @"{""type"":""game_started""}");

        AssertError(response, "unsupported_message");
    }

    [Theory]
    [InlineData(@"{""type"":""join_lobby"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""join_lobby"",""payload"":{""sessionId"":""session_1""}}")]
    [InlineData(@"{""type"":""leave_lobby"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""leave_lobby"",""payload"":{""sessionId"":""session_1""}}")]
    [InlineData(@"{""type"":""start_game"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""start_game"",""payload"":{""sessionId"":""session_1""}}")]
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

    [Fact]
    public void CleanupConnection_AfterGameStartDoesNotRemoveGamePlayers()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var firstContext = new LobbyConnectionContext("connection_1");
        var secondContext = new LobbyConnectionContext("connection_2");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), firstContext);
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_2"), secondContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_1", isReady: true), firstContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_2", isReady: true), secondContext);
        _ = handler.HandleTextMessage(StartGameMessage(session.SessionId, "player_1"), firstContext);

        handler.CleanupConnection(firstContext);

        var updatedSession = sessionManager.GetSession(session.SessionId);
        Assert.Equal(2, updatedSession?.Players.Count);
        Assert.Equal(2, updatedSession?.GameState.Players.Count);
        Assert.False(firstContext.IsBound);
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

    private static void AssertGameStartedPlayer(JsonElement player, string expectedPlayerId)
    {
        Assert.Equal(expectedPlayerId, player.GetProperty("playerId").GetString());
        Assert.Equal(expectedPlayerId, player.GetProperty("username").GetString());
        Assert.Equal($"token_{expectedPlayerId}", player.GetProperty("tokenId").GetString());
        Assert.Equal($"color_{expectedPlayerId}", player.GetProperty("colorId").GetString());
        Assert.Equal("start", player.GetProperty("currentTileId").GetString());
        Assert.Equal(1500, player.GetProperty("money").GetInt32());
    }

    private static string JoinMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""join_lobby"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string LeaveMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""leave_lobby"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string SetReadyMessage(string sessionId, string playerId, bool isReady)
    {
        var readyJson = isReady ? "true" : "false";
        return $@"{{""type"":""set_ready"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}"",""isReady"":{readyJson}}}}}";
    }

    private static string StartGameMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""start_game"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }
}
