namespace MonoJoey.Server.Tests.Sessions;

using MonoJoey.Server.GameEngine;
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
    public void SetReady_MarksTargetPlayerReadyAndPreservesOthers()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var firstPlayer = CreatePlayerConnection("player_1");
        var secondPlayer = CreatePlayerConnection("player_2");
        _ = sessionManager.JoinSession(session.SessionId, firstPlayer);
        _ = sessionManager.JoinSession(session.SessionId, secondPlayer);

        var updatedSession = sessionManager.SetReady(session.SessionId, firstPlayer.PlayerId, isReady: true);

        Assert.True(updatedSession.Players[0].IsReady);
        Assert.False(updatedSession.Players[1].IsReady);
        Assert.Equal(secondPlayer, updatedSession.Players[1]);
    }

    [Fact]
    public void SetReady_CanSetReadyBackToFalse()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var player = CreatePlayerConnection("player_1");
        _ = sessionManager.JoinSession(session.SessionId, player);
        _ = sessionManager.SetReady(session.SessionId, player.PlayerId, isReady: true);

        var updatedSession = sessionManager.SetReady(session.SessionId, player.PlayerId, isReady: false);

        Assert.False(updatedSession.Players[0].IsReady);
    }

    [Fact]
    public void SetReady_InvalidSessionThrows()
    {
        var sessionManager = new SessionManager();

        var exception = Assert.Throws<InvalidOperationException>(
            () => sessionManager.SetReady("missing_session", new PlayerId("player_1"), isReady: true));

        Assert.Equal("Session not found.", exception.Message);
    }

    [Fact]
    public void SetReady_PlayerNotInLobbyThrows()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();

        var exception = Assert.Throws<InvalidOperationException>(
            () => sessionManager.SetReady(session.SessionId, new PlayerId("player_1"), isReady: true));

        Assert.Equal("Player is not in lobby.", exception.Message);
    }

    [Fact]
    public void SetReady_StartedSessionThrows()
    {
        var sessionManager = new SessionManager();
        var startedSession = CreateReadyStartedSession(sessionManager);

        var exception = Assert.Throws<InvalidOperationException>(
            () => sessionManager.SetReady(startedSession.SessionId, new PlayerId("player_1"), isReady: false));

        Assert.Equal("Session is not in lobby status.", exception.Message);
    }

    [Fact]
    public void StartGame_RejectsFewerThanTwoPlayers()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var player = CreatePlayerConnection("player_1", isReady: true);
        _ = sessionManager.JoinSession(session.SessionId, player);

        var exception = Assert.Throws<InvalidOperationException>(
            () => sessionManager.StartGame(session.SessionId));

        Assert.Equal("Not enough players to start the game.", exception.Message);
    }

    [Fact]
    public void StartGame_RejectsWhenAnyPlayerIsNotReady()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(session.SessionId, CreatePlayerConnection("player_1", isReady: true));
        _ = sessionManager.JoinSession(session.SessionId, CreatePlayerConnection("player_2"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => sessionManager.StartGame(session.SessionId));

        Assert.Equal("All players must be ready to start the game.", exception.Message);
    }

    [Fact]
    public void StartGame_TransitionsToInGameAndInitializesPlayersInLobbyOrder()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(session.SessionId, CreatePlayerConnection("player_1", isReady: true));
        _ = sessionManager.JoinSession(session.SessionId, CreatePlayerConnection("player_2", isReady: true));

        var startedSession = sessionManager.StartGame(session.SessionId);

        Assert.Equal(GameSessionStatus.InGame, startedSession.Status);
        Assert.Equal(GamePhase.AwaitingRoll, startedSession.GameState.Phase);
        Assert.Equal(1, startedSession.GameState.TurnNumber);
        Assert.Equal("player_1", startedSession.GameState.CurrentTurnPlayerId?.Value);
        Assert.False(startedSession.GameState.HasRolledThisTurn);
        Assert.Same(session.GameState.Board, startedSession.GameState.Board);

        Assert.Collection(
            startedSession.GameState.Players,
            player => AssertStartedPlayer(player, "player_1"),
            player => AssertStartedPlayer(player, "player_2"));
    }

    [Fact]
    public void StartGame_InvalidSessionThrows()
    {
        var sessionManager = new SessionManager();

        var exception = Assert.Throws<InvalidOperationException>(
            () => sessionManager.StartGame("missing_session"));

        Assert.Equal("Session not found.", exception.Message);
    }

    [Fact]
    public void StartGame_StartedSessionThrows()
    {
        var sessionManager = new SessionManager();
        var startedSession = CreateReadyStartedSession(sessionManager);

        var exception = Assert.Throws<InvalidOperationException>(
            () => sessionManager.StartGame(startedSession.SessionId));

        Assert.Equal("Session is not in lobby status.", exception.Message);
    }

    [Fact]
    public void JoinSession_StartedSessionThrows()
    {
        var sessionManager = new SessionManager();
        var startedSession = CreateReadyStartedSession(sessionManager);

        var exception = Assert.Throws<InvalidOperationException>(
            () => sessionManager.JoinSession(startedSession.SessionId, CreatePlayerConnection("player_3")));

        Assert.Equal("Session is not in lobby status.", exception.Message);
    }

    [Fact]
    public void LeaveSession_AfterStartDoesNotRemoveGameStatePlayer()
    {
        var sessionManager = new SessionManager();
        var startedSession = CreateReadyStartedSession(sessionManager);

        var updatedSession = sessionManager.LeaveSession(
            startedSession.SessionId,
            CreatePlayerConnection("player_1", isReady: true));

        Assert.Single(updatedSession.Players);
        Assert.Equal(2, updatedSession.GameState.Players.Count);
        Assert.Contains(updatedSession.GameState.Players, player => player.PlayerId.Value == "player_1");
    }

    [Fact]
    public void GetSession_InvalidSessionReturnsNull()
    {
        var sessionManager = new SessionManager();

        var session = sessionManager.GetSession("missing_session");

        Assert.Null(session);
    }

    [Fact]
    public void UpdateGameState_ReplacesSessionGameState()
    {
        var sessionManager = new SessionManager();
        var startedSession = CreateReadyStartedSession(sessionManager);
        var updatedGameState = startedSession.GameState with { HasRolledThisTurn = true };

        var updatedSession = sessionManager.UpdateGameState(startedSession.SessionId, updatedGameState);

        Assert.Same(updatedGameState, updatedSession.GameState);
        Assert.Same(updatedGameState, sessionManager.GetSession(startedSession.SessionId)?.GameState);
        Assert.Equal(GameSessionStatus.InGame, updatedSession.Status);
    }

    [Fact]
    public void UpdateGameState_InvalidSessionThrows()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();

        var exception = Assert.Throws<InvalidOperationException>(
            () => sessionManager.UpdateGameState("missing_session", session.GameState));

        Assert.Equal("Session not found.", exception.Message);
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

    private static GameSession CreateReadyStartedSession(SessionManager sessionManager)
    {
        var session = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(session.SessionId, CreatePlayerConnection("player_1", isReady: true));
        _ = sessionManager.JoinSession(session.SessionId, CreatePlayerConnection("player_2", isReady: true));

        return sessionManager.StartGame(session.SessionId);
    }

    private static void AssertStartedPlayer(Player player, string expectedPlayerId)
    {
        Assert.Equal(expectedPlayerId, player.PlayerId.Value);
        Assert.Equal(expectedPlayerId, player.Username);
        Assert.Equal($"token_{expectedPlayerId}", player.TokenId);
        Assert.Equal($"color_{expectedPlayerId}", player.ColorId);
        Assert.Equal("start", player.CurrentTileId.Value);
        Assert.Equal(new Money(1500), player.Money);
        Assert.Empty(player.OwnedPropertyIds);
        Assert.Empty(player.HeldCardIds);
        Assert.False(player.IsBankrupt);
        Assert.False(player.IsEliminated);
    }

    private static PlayerConnection CreatePlayerConnection(string playerId, bool isReady = false)
    {
        return new PlayerConnection(
            new PlayerId(playerId),
            $"connection_{playerId}",
            isReady);
    }
}
