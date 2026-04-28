namespace MonoJoey.Server.Tests.Realtime;

using System.Text.Json;
using MonoJoey.Server.GameEngine;
using MonoJoey.Server.Realtime;
using MonoJoey.Server.Sessions;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

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
    public void RollDice_ReturnsRollResultAndUpdatesPlayerPosition()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(3, 5));
        var started = StartReadyGame(sessionManager, handler);

        using var response = Handle(
            handler,
            started.FirstContext,
            RollDiceMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "roll_result");
        var dice = payload.GetProperty("dice").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        var updatedSession = sessionManager.GetSession(started.Session.SessionId);

        Assert.Equal("player_1", payload.GetProperty("playerId").GetString());
        Assert.Equal(new[] { 3, 5 }, dice);
        Assert.Equal("table_01", payload.GetProperty("newPosition").GetString());
        Assert.False(payload.GetProperty("passedStart").GetBoolean());
        Assert.True(payload.GetProperty("hasRolledThisTurn").GetBoolean());
        Assert.Equal("table_01", updatedSession?.GameState.Players[0].CurrentTileId.Value);
        Assert.True(updatedSession?.GameState.HasRolledThisTurn);
        Assert.False(updatedSession?.GameState.HasResolvedTileThisTurn);
        Assert.Equal(GamePhase.AwaitingRoll, updatedSession?.GameState.Phase);
    }

    [Fact]
    public void RollDice_ReturnsPassedStartFromMovement()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 1));
        var started = StartReadyGame(sessionManager, handler);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            player => player with { CurrentTileId = new TileId("go_to_lockup_01") });

        using var response = Handle(
            handler,
            started.FirstContext,
            RollDiceMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "roll_result");

        Assert.Equal("property_01", payload.GetProperty("newPosition").GetString());
        Assert.True(payload.GetProperty("passedStart").GetBoolean());
    }

    [Fact]
    public void ResolveTile_ReturnsResolveTileResultAfterRoll()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(3, 5));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);

        using var response = Handle(
            handler,
            started.FirstContext,
            ResolveTileMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "resolve_tile_result");

        Assert.Equal("player_1", payload.GetProperty("playerId").GetString());
        Assert.Equal("table_01", payload.GetProperty("tileId").GetString());
        Assert.Equal(8, payload.GetProperty("tileIndex").GetInt32());
        Assert.Equal("table_deck", payload.GetProperty("tileType").GetString());
        Assert.True(payload.GetProperty("requiresAction").GetBoolean());
        Assert.Equal("deck_placeholder", payload.GetProperty("actionKind").GetString());
    }

    [Theory]
    [InlineData("property_01", true, "property_placeholder")]
    [InlineData("chance_01", true, "deck_placeholder")]
    [InlineData("tax_01", true, "tax_placeholder")]
    [InlineData("go_to_lockup_01", true, "go_to_lockup_placeholder")]
    [InlineData("start", false, "start_placeholder")]
    [InlineData("free_space_01", false, "no_action")]
    public void ResolveTile_MapsRequiresActionFromTileClassification(
        string tileId,
        bool expectedRequiresAction,
        string expectedActionKind)
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            player => player with { CurrentTileId = new TileId(tileId) });
        var session = sessionManager.GetSession(started.Session.SessionId)!;
        _ = sessionManager.UpdateGameState(
            session.SessionId,
            session.GameState with { HasRolledThisTurn = true });

        using var response = Handle(
            handler,
            started.FirstContext,
            ResolveTileMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "resolve_tile_result");

        Assert.Equal(expectedRequiresAction, payload.GetProperty("requiresAction").GetBoolean());
        Assert.Equal(expectedActionKind, payload.GetProperty("actionKind").GetString());
    }

    [Fact]
    public void ResolveTile_BeforeRollReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);

        using var response = Handle(
            handler,
            started.FirstContext,
            ResolveTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void ResolveTile_SecondResolveInSameTurnReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);
        _ = handler.HandleTextMessage(ResolveTileMessage(started.Session.SessionId, "player_1"), started.FirstContext);

        using var response = Handle(
            handler,
            started.FirstContext,
            ResolveTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void ResolveTile_InvalidSessionReturnsInvalidSession()
    {
        var handler = CreateHandler(new SessionManager(), new DiceRoll(1, 2));
        var context = new LobbyConnectionContext("connection_1");
        context.Bind("missing_session", "player_1");

        using var response = Handle(handler, context, ResolveTileMessage("missing_session", "player_1"));

        AssertError(response, "invalid_session");
    }

    [Fact]
    public void ResolveTile_UnboundContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);
        var unboundContext = new LobbyConnectionContext("connection_3");

        using var response = Handle(
            handler,
            unboundContext,
            ResolveTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void ResolveTile_WrongPlayerContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);

        using var response = Handle(
            handler,
            started.SecondContext,
            ResolveTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void ResolveTile_LobbySessionReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(handler, context, ResolveTileMessage(session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void ResolveTile_PlayerMissingFromGameStateReturnsPlayerNotFound()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);
        var session = sessionManager.GetSession(started.Session.SessionId)!;
        _ = sessionManager.UpdateGameState(
            session.SessionId,
            session.GameState with
            {
                Players = session.GameState.Players
                    .Where(player => player.PlayerId.Value != "player_1")
                    .ToArray(),
            });

        using var response = Handle(
            handler,
            started.FirstContext,
            ResolveTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_not_found");
    }

    [Fact]
    public void ResolveTile_LockedCurrentPlayerReturnsPlayerLocked()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            player => player with { IsLockedUp = true });

        using var response = Handle(
            handler,
            started.FirstContext,
            ResolveTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_locked");
    }

    [Fact]
    public void ResolveTile_SetsResolvedFlagAndPreservesUnrelatedState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);
        var beforeResolve = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            ResolveTileMessage(started.Session.SessionId, "player_1"));
        var afterResolve = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        AssertResponseType(response, "resolve_tile_result");
        Assert.True(afterResolve.HasRolledThisTurn);
        Assert.True(afterResolve.HasResolvedTileThisTurn);
        Assert.Equal(beforeResolve.Phase, afterResolve.Phase);
        Assert.Equal(beforeResolve.CurrentTurnPlayerId, afterResolve.CurrentTurnPlayerId);
        Assert.Equal(beforeResolve.TurnNumber, afterResolve.TurnNumber);
        Assert.Equal(beforeResolve.Players[0].CurrentTileId, afterResolve.Players[0].CurrentTileId);
        Assert.Equal(beforeResolve.Players[0].Money, afterResolve.Players[0].Money);
        Assert.Equal(beforeResolve.Players[0].OwnedPropertyIds, afterResolve.Players[0].OwnedPropertyIds);
        Assert.Equal(beforeResolve.Players[0].HeldCardIds, afterResolve.Players[0].HeldCardIds);
    }

    [Fact]
    public void RollDice_SecondRollInSameTurnReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);

        using var response = Handle(
            handler,
            started.FirstContext,
            RollDiceMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void RollDice_NonCurrentPlayerReturnsNotYourTurn()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);

        using var response = Handle(
            handler,
            started.SecondContext,
            RollDiceMessage(started.Session.SessionId, "player_2"));

        AssertError(response, "not_your_turn");
    }

    [Fact]
    public void RollDice_EliminatedCurrentPlayerReturnsPlayerEliminated()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            player => player with { IsEliminated = true });

        using var response = Handle(
            handler,
            started.FirstContext,
            RollDiceMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_eliminated");
    }

    [Fact]
    public void RollDice_LockedCurrentPlayerReturnsPlayerLocked()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            player => player with { IsLockedUp = true });

        using var response = Handle(
            handler,
            started.FirstContext,
            RollDiceMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_locked");
    }

    [Fact]
    public void RollDice_InvalidSessionReturnsInvalidSession()
    {
        var handler = CreateHandler(new SessionManager(), new DiceRoll(1, 2));
        var context = new LobbyConnectionContext("connection_1");
        context.Bind("missing_session", "player_1");

        using var response = Handle(handler, context, RollDiceMessage("missing_session", "player_1"));

        AssertError(response, "invalid_session");
    }

    [Fact]
    public void RollDice_LobbySessionReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(handler, context, RollDiceMessage(session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void RollDice_PlayerMissingFromGameStateReturnsPlayerNotFound()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var session = sessionManager.GetSession(started.Session.SessionId)!;
        _ = sessionManager.UpdateGameState(
            session.SessionId,
            session.GameState with
            {
                Players = session.GameState.Players
                    .Where(player => player.PlayerId.Value != "player_1")
                    .ToArray(),
            });

        using var response = Handle(
            handler,
            started.FirstContext,
            RollDiceMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_not_found");
    }

    [Fact]
    public void RollDice_UnboundContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var unboundContext = new LobbyConnectionContext("connection_3");

        using var response = Handle(
            handler,
            unboundContext,
            RollDiceMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_switch_rejected");
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

    [Fact]
    public void ClientSentResolveTileResultReturnsUnsupportedMessage()
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, @"{""type"":""resolve_tile_result""}");

        AssertError(response, "unsupported_message");
    }

    [Theory]
    [InlineData(@"{""type"":""join_lobby"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""join_lobby"",""payload"":{""sessionId"":""session_1""}}")]
    [InlineData(@"{""type"":""leave_lobby"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""leave_lobby"",""payload"":{""sessionId"":""session_1""}}")]
    [InlineData(@"{""type"":""start_game"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""start_game"",""payload"":{""sessionId"":""session_1""}}")]
    [InlineData(@"{""type"":""roll_dice"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""roll_dice"",""payload"":{""sessionId"":""session_1""}}")]
    [InlineData(@"{""type"":""resolve_tile"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""resolve_tile"",""payload"":{""sessionId"":""session_1""}}")]
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

    private static LobbyMessageHandler CreateHandler(SessionManager sessionManager, DiceRoll diceRoll)
    {
        return new LobbyMessageHandler(sessionManager, new DiceService(new FixedDiceRoller(diceRoll)));
    }

    private static StartedRealtimeGame StartReadyGame(
        SessionManager sessionManager,
        LobbyMessageHandler handler)
    {
        var session = sessionManager.CreateSession();
        var firstContext = new LobbyConnectionContext("connection_1");
        var secondContext = new LobbyConnectionContext("connection_2");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), firstContext);
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_2"), secondContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_1", isReady: true), firstContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_2", isReady: true), secondContext);
        _ = handler.HandleTextMessage(StartGameMessage(session.SessionId, "player_1"), firstContext);

        return new StartedRealtimeGame(
            sessionManager.GetSession(session.SessionId)!,
            firstContext,
            secondContext);
    }

    private static GameSession UpdateEnginePlayer(
        SessionManager sessionManager,
        string sessionId,
        string playerId,
        Func<Player, Player> updatePlayer)
    {
        var session = sessionManager.GetSession(sessionId)!;
        var updatedPlayers = session.GameState.Players
            .Select(player => player.PlayerId.Value == playerId ? updatePlayer(player) : player)
            .ToArray();

        return sessionManager.UpdateGameState(
            sessionId,
            session.GameState with { Players = updatedPlayers });
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

    private static string RollDiceMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""roll_dice"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string ResolveTileMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""resolve_tile"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private sealed record StartedRealtimeGame(
        GameSession Session,
        LobbyConnectionContext FirstContext,
        LobbyConnectionContext SecondContext);

    private sealed class FixedDiceRoller : IDiceRoller
    {
        private readonly DiceRoll diceRoll;

        public FixedDiceRoller(DiceRoll diceRoll)
        {
            this.diceRoll = diceRoll;
        }

        public DiceRoll Roll()
        {
            return diceRoll;
        }
    }
}
