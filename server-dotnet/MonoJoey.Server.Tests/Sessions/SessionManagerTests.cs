namespace MonoJoey.Server.Tests.Sessions;

using MonoJoey.Server.Sessions;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class SessionManagerTests
{
    [Fact]
    public void CreateSession_CreatesLobbySessionWithGameState()
    {
        var sessionManager = new SessionManager();

        var session = sessionManager.CreateSession();

        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
        Assert.Empty(session.Players);
        Assert.Equal(GameSessionStatus.Lobby, session.Status);
        Assert.Equal(GamePhase.Lobby, session.GameState.Phase);
        Assert.Equal(session.SessionId, session.GameState.MatchId.Value);
        Assert.Empty(session.GameState.Players);
        Assert.Null(session.GameState.CurrentTurnPlayerId);
        Assert.Equal(0, session.GameState.TurnNumber);
        Assert.Same(session, sessionManager.GetSession(session.SessionId));
    }

    [Fact]
    public void JoinSession_AddsPlayerToSession()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var player = CreatePlayerConnection("player_1");

        var updatedSession = sessionManager.JoinSession(session.SessionId, player);

        Assert.Single(updatedSession.Players);
        Assert.Equal(player, updatedSession.Players[0]);
        Assert.Equal(player, sessionManager.GetSession(session.SessionId)?.Players[0]);
    }

    [Fact]
    public void LeaveSession_RemovesPlayerFromSession()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var player = CreatePlayerConnection("player_1");

        _ = sessionManager.JoinSession(session.SessionId, player);
        var updatedSession = sessionManager.LeaveSession(session.SessionId, player);

        Assert.Empty(updatedSession.Players);
        Assert.Empty(sessionManager.GetSession(session.SessionId)?.Players ?? Array.Empty<PlayerConnection>());
    }

    [Fact]
    public void JoinAndLeaveSession_PlayerListUpdatesCorrectly()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var firstPlayer = CreatePlayerConnection("player_1");
        var secondPlayer = CreatePlayerConnection("player_2");

        _ = sessionManager.JoinSession(session.SessionId, firstPlayer);
        _ = sessionManager.JoinSession(session.SessionId, secondPlayer);
        var updatedSession = sessionManager.LeaveSession(session.SessionId, firstPlayer);

        Assert.Single(updatedSession.Players);
        Assert.Equal(secondPlayer.PlayerId, updatedSession.Players[0].PlayerId);
    }

    [Fact]
    public void JoinSession_DuplicatePlayerJoinDoesNotAddDuplicate()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var player = CreatePlayerConnection("player_1");
        var duplicatePlayer = player with
        {
            ConnectionId = "connection_rejoin",
            IsReady = true,
        };

        _ = sessionManager.JoinSession(session.SessionId, player);
        var updatedSession = sessionManager.JoinSession(session.SessionId, duplicatePlayer);

        Assert.Single(updatedSession.Players);
        Assert.Equal(player, updatedSession.Players[0]);
    }

    [Fact]
    public void LeaveSession_PlayerNotInSessionIsNoOp()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var player = CreatePlayerConnection("player_1");

        var updatedSession = sessionManager.LeaveSession(session.SessionId, player);

        Assert.Empty(updatedSession.Players);
    }

    [Fact]
    public void GetSession_InvalidSessionReturnsNull()
    {
        var sessionManager = new SessionManager();

        var session = sessionManager.GetSession("missing_session");

        Assert.Null(session);
    }

    [Fact]
    public void JoinSession_InvalidSessionThrows()
    {
        var sessionManager = new SessionManager();
        var player = CreatePlayerConnection("player_1");

        var exception = Assert.Throws<InvalidOperationException>(
            () => sessionManager.JoinSession("missing_session", player));

        Assert.Equal("Session not found.", exception.Message);
    }

    [Fact]
    public void LeaveSession_InvalidSessionThrows()
    {
        var sessionManager = new SessionManager();
        var player = CreatePlayerConnection("player_1");

        var exception = Assert.Throws<InvalidOperationException>(
            () => sessionManager.LeaveSession("missing_session", player));

        Assert.Equal("Session not found.", exception.Message);
    }

    private static PlayerConnection CreatePlayerConnection(string playerId)
    {
        return new PlayerConnection(
            new PlayerId(playerId),
            $"connection_{playerId}",
            IsReady: false);
    }
}
