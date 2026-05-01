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
    public void RollDice_ResetsResolvedAndExecutedFlags()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = UpdateGameState(
            sessionManager,
            started.Session.SessionId,
            gameState => gameState with
            {
                HasResolvedTileThisTurn = true,
                HasExecutedTileThisTurn = true,
            });

        using var response = Handle(
            handler,
            started.FirstContext,
            RollDiceMessage(started.Session.SessionId, "player_1"));
        var updatedSession = sessionManager.GetSession(started.Session.SessionId)!;

        AssertResponseType(response, "roll_result");
        Assert.True(updatedSession.GameState.HasRolledThisTurn);
        Assert.False(updatedSession.GameState.HasResolvedTileThisTurn);
        Assert.False(updatedSession.GameState.HasExecutedTileThisTurn);
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
    public void ExecuteTile_UnownedPropertyStartsMandatoryAuctionAndPreservesPhase()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "property_01");
        var beforeExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "execute_tile_result");
        var auction = payload.GetProperty("auction");
        var updatedSession = sessionManager.GetSession(started.Session.SessionId)!;

        Assert.Equal("player_1", payload.GetProperty("playerId").GetString());
        Assert.Equal("property_01", payload.GetProperty("tileId").GetString());
        Assert.Equal(1, payload.GetProperty("tileIndex").GetInt32());
        Assert.Equal("property", payload.GetProperty("tileType").GetString());
        Assert.Equal("property_placeholder", payload.GetProperty("actionKind").GetString());
        Assert.Equal("auction_started", payload.GetProperty("executionKind").GetString());
        Assert.Equal("awaiting_roll", payload.GetProperty("phase").GetString());
        Assert.True(payload.GetProperty("hasExecutedTileThisTurn").GetBoolean());
        Assert.Equal("property_01", auction.GetProperty("propertyTileId").GetString());
        Assert.Equal("player_1", auction.GetProperty("triggeringPlayerId").GetString());
        Assert.Equal("awaiting_initial_bid", auction.GetProperty("status").GetString());
        Assert.Equal(0, auction.GetProperty("startingBid").GetInt32());
        Assert.Equal(1, auction.GetProperty("minimumBidIncrement").GetInt32());
        Assert.Equal(9, auction.GetProperty("initialPreBidSeconds").GetInt32());
        Assert.Equal(3, auction.GetProperty("bidResetSeconds").GetInt32());
        Assert.Equal(JsonValueKind.Null, auction.GetProperty("highestBid").ValueKind);
        Assert.Equal(JsonValueKind.Null, auction.GetProperty("highestBidderId").ValueKind);
        Assert.Equal(JsonValueKind.Null, auction.GetProperty("countdownDurationSeconds").ValueKind);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("rent").ValueKind);
        Assert.True(updatedSession.GameState.HasExecutedTileThisTurn);
        Assert.NotNull(updatedSession.GameState.ActiveAuctionState);
        Assert.Equal("property_01", updatedSession.GameState.ActiveAuctionState?.PropertyTileId.Value);
        Assert.Equal(beforeExecute.Phase, updatedSession.GameState.Phase);
        Assert.Equal(beforeExecute.Players[0].Money, updatedSession.GameState.Players[0].Money);
        Assert.Equal(beforeExecute.Players[1].Money, updatedSession.GameState.Players[1].Money);
    }

    [Fact]
    public void ExecuteTile_PropertyOwnedByAnotherPlayerPaysRentAndPreservesPhase()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var readySession = SetCurrentPlayerReadyToExecuteTile(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            "property_01");
        _ = UpdateGameState(
            sessionManager,
            readySession.SessionId,
            gameState => gameState with
            {
                Players = gameState.Players
                    .Select(player => player.PlayerId.Value == "player_2"
                        ? player with { OwnedPropertyIds = new HashSet<TileId> { new("property_01") } }
                        : player)
                    .ToArray(),
            });
        var beforeExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "execute_tile_result");
        var rent = payload.GetProperty("rent");
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("rent_paid", payload.GetProperty("executionKind").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("auction").ValueKind);
        Assert.Equal("player_1", rent.GetProperty("payerId").GetString());
        Assert.Equal("player_2", rent.GetProperty("ownerId").GetString());
        Assert.Equal(2, rent.GetProperty("rentDue").GetInt32());
        Assert.Equal(2, rent.GetProperty("rentPaid").GetInt32());
        Assert.Equal(1498, rent.GetProperty("payerMoney").GetInt32());
        Assert.Equal(1502, rent.GetProperty("ownerMoney").GetInt32());
        Assert.False(rent.GetProperty("playerEliminated").GetBoolean());
        Assert.Equal(JsonValueKind.Null, rent.GetProperty("eliminationReason").ValueKind);
        Assert.True(afterExecute.HasExecutedTileThisTurn);
        Assert.Null(afterExecute.ActiveAuctionState);
        Assert.Equal(beforeExecute.Phase, afterExecute.Phase);
    }

    [Fact]
    public void ExecuteTile_InsufficientRentEliminatesPayer()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var readySession = SetCurrentPlayerReadyToExecuteTile(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            "property_01");
        _ = UpdateGameState(
            sessionManager,
            readySession.SessionId,
            gameState => gameState with
            {
                Players = gameState.Players
                    .Select(player => player.PlayerId.Value switch
                    {
                        "player_1" => player with { Money = new Money(1) },
                        "player_2" => player with { OwnedPropertyIds = new HashSet<TileId> { new("property_01") } },
                        _ => player,
                    })
                    .ToArray(),
            });

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "execute_tile_result");
        var rent = payload.GetProperty("rent");
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("rent_unpaid_player_eliminated", payload.GetProperty("executionKind").GetString());
        Assert.Equal(2, rent.GetProperty("rentDue").GetInt32());
        Assert.Equal(0, rent.GetProperty("rentPaid").GetInt32());
        Assert.Equal(1, rent.GetProperty("payerMoney").GetInt32());
        Assert.Equal(1500, rent.GetProperty("ownerMoney").GetInt32());
        Assert.True(rent.GetProperty("playerEliminated").GetBoolean());
        Assert.Equal("cannot_fulfill_payment", rent.GetProperty("eliminationReason").GetString());
        Assert.True(afterExecute.Players[0].IsEliminated);
        Assert.True(afterExecute.HasExecutedTileThisTurn);
    }

    [Fact]
    public void ExecuteTile_SelfOwnedPropertyDoesNotChargeRent()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var readySession = SetCurrentPlayerReadyToExecuteTile(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            "property_01");
        _ = UpdateGameState(
            sessionManager,
            readySession.SessionId,
            gameState => gameState with
            {
                Players = gameState.Players
                    .Select(player => player.PlayerId.Value == "player_1"
                        ? player with { OwnedPropertyIds = new HashSet<TileId> { new("property_01") } }
                        : player)
                    .ToArray(),
            });
        var beforeExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "execute_tile_result");
        var rent = payload.GetProperty("rent");
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("rent_not_charged", payload.GetProperty("executionKind").GetString());
        Assert.Equal("player_1", rent.GetProperty("ownerId").GetString());
        Assert.Equal(0, rent.GetProperty("rentDue").GetInt32());
        Assert.Equal(0, rent.GetProperty("rentPaid").GetInt32());
        Assert.Equal(1500, rent.GetProperty("payerMoney").GetInt32());
        Assert.Equal(1500, rent.GetProperty("ownerMoney").GetInt32());
        Assert.False(rent.GetProperty("playerEliminated").GetBoolean());
        Assert.Equal(beforeExecute.Players[0].Money, afterExecute.Players[0].Money);
        Assert.Equal(beforeExecute.Phase, afterExecute.Phase);
    }

    [Theory]
    [InlineData("start", "start_placeholder")]
    [InlineData("free_space_01", "no_action")]
    public void ExecuteTile_NoActionTilesOnlyMarkExecutedAndPreservePhase(string tileId, string actionKind)
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", tileId);
        var beforeExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "execute_tile_result");
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal(actionKind, payload.GetProperty("actionKind").GetString());
        Assert.Equal("no_action", payload.GetProperty("executionKind").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("auction").ValueKind);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("rent").ValueKind);
        Assert.True(afterExecute.HasExecutedTileThisTurn);
        Assert.Null(afterExecute.ActiveAuctionState);
        Assert.Equal(beforeExecute.Phase, afterExecute.Phase);
        Assert.Equal(beforeExecute.Players[0].Money, afterExecute.Players[0].Money);
        Assert.Equal(beforeExecute.Players[0].OwnedPropertyIds, afterExecute.Players[0].OwnedPropertyIds);
    }

    [Fact]
    public void ExecuteTile_ChanceDeckDrawsExecutesPersistsDeckAndReturnsCardPayload()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "chance_01");
        var beforeExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "execute_tile_result");
        var card = payload.GetProperty("card");
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;
        var chanceDeckState = afterExecute.CardDeckStates[CardDeckIds.Chance];

        Assert.Equal("deck_placeholder", payload.GetProperty("actionKind").GetString());
        Assert.Equal("card_executed", payload.GetProperty("executionKind").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("auction").ValueKind);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("rent").ValueKind);
        Assert.Equal(CardDeckIds.Chance, card.GetProperty("deckId").GetString());
        Assert.Equal("CHANCE_01_MOVE_TO_START", card.GetProperty("cardId").GetString());
        Assert.Equal("CHANCE_01_MOVE_TO_START", card.GetProperty("displayName").GetString());
        Assert.Equal("move_to_start", card.GetProperty("resolutionKind").GetString());
        Assert.Equal("card_executed", card.GetProperty("executionKind").GetString());
        Assert.Equal("player_1", card.GetProperty("playerId").GetString());
        Assert.Equal("start", card.GetProperty("currentTileId").GetString());
        Assert.Equal(1500, card.GetProperty("money").GetInt32());
        Assert.False(card.GetProperty("isEliminated").GetBoolean());
        Assert.False(card.GetProperty("isLockedUp").GetBoolean());
        Assert.Empty(card.GetProperty("heldCardIds").EnumerateArray());
        Assert.True(afterExecute.HasExecutedTileThisTurn);
        Assert.Equal("start", afterExecute.Players[0].CurrentTileId.Value);
        Assert.Equal(15, chanceDeckState.DrawPile.Count);
        Assert.Equal("CHANCE_02_MOVE_TO_EARLY_PROPERTY", chanceDeckState.DrawPile[0].CardId.Value);
        Assert.Single(chanceDeckState.DiscardPile);
        Assert.Equal("CHANCE_01_MOVE_TO_START", chanceDeckState.DiscardPile[0].CardId.Value);
        Assert.Same(beforeExecute.CardDeckStates[CardDeckIds.Table], afterExecute.CardDeckStates[CardDeckIds.Table]);
    }

    [Fact]
    public void ExecuteTile_TableDeckDrawsFromTableAndUpdatesMoney()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "table_01");

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "execute_tile_result");
        var card = payload.GetProperty("card");
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("card_executed", payload.GetProperty("executionKind").GetString());
        Assert.Equal(CardDeckIds.Table, card.GetProperty("deckId").GetString());
        Assert.Equal("TABLE_01_RECEIVE_FROM_BANK", card.GetProperty("cardId").GetString());
        Assert.Equal("receive_money", card.GetProperty("resolutionKind").GetString());
        Assert.Equal(1525, card.GetProperty("money").GetInt32());
        Assert.Equal(new Money(1525), afterExecute.Players[0].Money);
        Assert.Equal(15, afterExecute.CardDeckStates[CardDeckIds.Table].DrawPile.Count);
        Assert.Single(afterExecute.CardDeckStates[CardDeckIds.Table].DiscardPile);
        Assert.Equal("TABLE_01_RECEIVE_FROM_BANK", afterExecute.CardDeckStates[CardDeckIds.Table].DiscardPile[0].CardId.Value);
        Assert.Equal(PlaceholderCardDeckFactory.ChanceDeckCardCount, afterExecute.CardDeckStates[CardDeckIds.Chance].DrawPile.Count);
    }

    [Fact]
    public void ExecuteTile_HeldLockupEscapeCardPersistsWithoutDiscardAcrossTurns()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "chance_01");
        var holdCard = CreateCard("TEST_HOLD_ESCAPE", CardActionKind.HoldForLater);
        _ = UpdateDeckState(
            sessionManager,
            started.Session.SessionId,
            CardDeckIds.Chance,
            new CardDeckState(CardDeckIds.Chance, new[] { holdCard }, Array.Empty<Card>()));

        using var executeResponse = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var executePayload = AssertResponseType(executeResponse, "execute_tile_result");
        var card = executePayload.GetProperty("card");
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("card_held", executePayload.GetProperty("executionKind").GetString());
        Assert.Equal("get_out_of_lockup", card.GetProperty("resolutionKind").GetString());
        Assert.Equal("TEST_HOLD_ESCAPE", Assert.Single(card.GetProperty("heldCardIds").EnumerateArray()).GetString());
        Assert.Contains(new CardId("TEST_HOLD_ESCAPE"), afterExecute.Players[0].HeldCardIds);
        Assert.Empty(afterExecute.CardDeckStates[CardDeckIds.Chance].DrawPile);
        Assert.Empty(afterExecute.CardDeckStates[CardDeckIds.Chance].DiscardPile);

        using var endTurnResponse = Handle(
            handler,
            started.FirstContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));
        AssertResponseType(endTurnResponse, "end_turn_result");
        var afterEndTurn = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Contains(new CardId("TEST_HOLD_ESCAPE"), afterEndTurn.Players[0].HeldCardIds);
        Assert.Empty(afterEndTurn.CardDeckStates[CardDeckIds.Chance].DiscardPile);
    }

    [Fact]
    public void ExecuteTile_PayBankCardCanEliminatePlayerAndBlockEndTurn()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "chance_01");
        var payCard = CreateCard(
            "TEST_PAY_BANK",
            CardActionKind.PayBank,
            new CardActionParameters(Amount: new Money(15)));
        _ = UpdateGameState(
            sessionManager,
            started.Session.SessionId,
            gameState => gameState with
            {
                Players = gameState.Players
                    .Select(player => player.PlayerId.Value == "player_1"
                        ? player with { Money = new Money(10) }
                        : player)
                    .ToArray(),
            });
        _ = UpdateDeckState(
            sessionManager,
            started.Session.SessionId,
            CardDeckIds.Chance,
            new CardDeckState(CardDeckIds.Chance, new[] { payCard }, Array.Empty<Card>()));

        using var executeResponse = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var executePayload = AssertResponseType(executeResponse, "execute_tile_result");
        var card = executePayload.GetProperty("card");
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("card_payment_eliminated_player", executePayload.GetProperty("executionKind").GetString());
        Assert.Equal("pay_money", card.GetProperty("resolutionKind").GetString());
        Assert.Equal(-5, card.GetProperty("money").GetInt32());
        Assert.True(card.GetProperty("isEliminated").GetBoolean());
        Assert.True(afterExecute.Players[0].IsEliminated);
        Assert.True(afterExecute.HasExecutedTileThisTurn);
        Assert.Single(afterExecute.CardDeckStates[CardDeckIds.Chance].DiscardPile);

        using var endTurnResponse = Handle(
            handler,
            started.FirstContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));

        AssertError(endTurnResponse, "player_eliminated");
    }

    [Fact]
    public void ExecuteTile_GoToLockupCardLocksPlayerAndStillAllowsEndTurn()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "chance_01");
        var lockupCard = CreateCard("TEST_GO_TO_LOCKUP", CardActionKind.GoToLockup);
        _ = UpdateDeckState(
            sessionManager,
            started.Session.SessionId,
            CardDeckIds.Chance,
            new CardDeckState(CardDeckIds.Chance, new[] { lockupCard }, Array.Empty<Card>()));

        using var executeResponse = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var executePayload = AssertResponseType(executeResponse, "execute_tile_result");
        var card = executePayload.GetProperty("card");
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("card_executed", executePayload.GetProperty("executionKind").GetString());
        Assert.Equal("go_to_lockup", card.GetProperty("resolutionKind").GetString());
        Assert.True(card.GetProperty("isLockedUp").GetBoolean());
        Assert.True(afterExecute.Players[0].IsLockedUp);
        Assert.True(afterExecute.HasExecutedTileThisTurn);

        using var endTurnResponse = Handle(
            handler,
            started.FirstContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));
        var endTurnPayload = AssertResponseType(endTurnResponse, "end_turn_result");
        var afterEndTurn = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("player_2", endTurnPayload.GetProperty("nextPlayerId").GetString());
        Assert.Equal("player_2", afterEndTurn.CurrentTurnPlayerId?.Value);
    }

    [Fact]
    public void ExecuteTile_MissingDeckReturnsCardDeckNotFoundWithoutMutation()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "chance_01");
        _ = UpdateGameState(
            sessionManager,
            started.Session.SessionId,
            gameState => gameState with
            {
                CardDeckStates = gameState.CardDeckStates
                    .Where(entry => entry.Key != CardDeckIds.Chance)
                    .ToDictionary(entry => entry.Key, entry => entry.Value),
            });
        var beforeExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        AssertError(response, "card_deck_not_found");
        Assert.Same(beforeExecute, afterExecute);
    }

    [Fact]
    public void ExecuteTile_EmptyDrawPileReturnsCardDeckEmptyWithoutMutation()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "chance_01");
        _ = UpdateDeckState(
            sessionManager,
            started.Session.SessionId,
            CardDeckIds.Chance,
            new CardDeckState(
                CardDeckIds.Chance,
                Array.Empty<Card>(),
                new[] { CreateCard("DISCARDED", CardActionKind.ReceiveFromBank) }));
        var beforeExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        AssertError(response, "card_deck_empty");
        Assert.Same(beforeExecute, afterExecute);
    }

    [Fact]
    public void ExecuteTile_UnsupportedResolvedCardActionReturnsUnsupportedCardActionWithoutMutation()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "chance_01");
        var unsupportedCard = CreateCard(
            "TEST_MOVE_TO_TILE",
            CardActionKind.MoveToTile,
            new CardActionParameters(TargetTileId: new TileId("property_01")));
        _ = UpdateDeckState(
            sessionManager,
            started.Session.SessionId,
            CardDeckIds.Chance,
            new CardDeckState(CardDeckIds.Chance, new[] { unsupportedCard }, Array.Empty<Card>()));
        var beforeExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        AssertError(response, "unsupported_card_action");
        Assert.Same(beforeExecute, afterExecute);
    }

    [Fact]
    public void ExecuteTile_InvalidCardReturnsInvalidCardWithoutMutation()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "chance_01");
        var invalidCard = CreateCard("TEST_INVALID", CardActionKind.ReceiveFromBank);
        _ = UpdateDeckState(
            sessionManager,
            started.Session.SessionId,
            CardDeckIds.Chance,
            new CardDeckState(CardDeckIds.Chance, new[] { invalidCard }, Array.Empty<Card>()));
        var beforeExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        AssertError(response, "invalid_card");
        Assert.Same(beforeExecute, afterExecute);
    }

    [Theory]
    [InlineData("tax_01")]
    [InlineData("go_to_lockup_01")]
    public void ExecuteTile_UnsupportedTileEffectsReturnUnsupportedTileEffectWithoutMutation(string tileId)
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", tileId);
        var beforeExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));
        var afterExecute = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        AssertError(response, "unsupported_tile_effect");
        Assert.Same(beforeExecute, afterExecute);
    }

    [Fact]
    public void ExecuteTile_BeforeRollReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void ExecuteTile_BeforeResolveReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void ExecuteTile_SecondExecuteInSameTurnReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "start");
        _ = handler.HandleTextMessage(ExecuteTileMessage(started.Session.SessionId, "player_1"), started.FirstContext);

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void ExecuteTile_ActiveAuctionAlreadyStoredReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var readySession = SetCurrentPlayerReadyToExecuteTile(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            "property_01");
        var auction = AuctionManager.StartMandatoryAuction(
            readySession.GameState,
            new PlayerId("player_1"),
            new TileId("property_01")).AuctionState;
        _ = UpdateGameState(
            sessionManager,
            readySession.SessionId,
            gameState => gameState with { ActiveAuctionState = auction });

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void ExecuteTile_InvalidSessionReturnsInvalidSession()
    {
        var handler = CreateHandler(new SessionManager(), new DiceRoll(1, 2));
        var context = new LobbyConnectionContext("connection_1");
        context.Bind("missing_session", "player_1");

        using var response = Handle(handler, context, ExecuteTileMessage("missing_session", "player_1"));

        AssertError(response, "invalid_session");
    }

    [Fact]
    public void ExecuteTile_UnboundContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "start");
        var unboundContext = new LobbyConnectionContext("connection_3");

        using var response = Handle(
            handler,
            unboundContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void ExecuteTile_WrongPlayerContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "start");

        using var response = Handle(
            handler,
            started.SecondContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void ExecuteTile_LobbySessionReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(handler, context, ExecuteTileMessage(session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void ExecuteTile_PlayerMissingFromGameStateReturnsPlayerNotFound()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "start");
        _ = UpdateGameState(
            sessionManager,
            started.Session.SessionId,
            gameState => gameState with
            {
                Players = gameState.Players
                    .Where(player => player.PlayerId.Value != "player_1")
                    .ToArray(),
            });

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_not_found");
    }

    [Fact]
    public void ExecuteTile_NonCurrentPlayerReturnsNotYourTurn()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_2", "start");

        using var response = Handle(
            handler,
            started.SecondContext,
            ExecuteTileMessage(started.Session.SessionId, "player_2"));

        AssertError(response, "not_your_turn");
    }

    [Fact]
    public void ExecuteTile_EliminatedCurrentPlayerReturnsPlayerEliminated()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "start");
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            player => player with { IsEliminated = true });

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_eliminated");
    }

    [Fact]
    public void ExecuteTile_LockedCurrentPlayerReturnsPlayerLocked()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "start");
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            player => player with { IsLockedUp = true });

        using var response = Handle(
            handler,
            started.FirstContext,
            ExecuteTileMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_locked");
    }

    [Fact]
    public void EndTurn_AfterRollResolveExecuteReturnsResultAndAdvancesTurn()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(3, 3));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);
        _ = handler.HandleTextMessage(ResolveTileMessage(started.Session.SessionId, "player_1"), started.FirstContext);
        _ = handler.HandleTextMessage(ExecuteTileMessage(started.Session.SessionId, "player_1"), started.FirstContext);
        var beforeEnd = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "end_turn_result");
        var afterEnd = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("player_1", payload.GetProperty("previousPlayerId").GetString());
        Assert.Equal("player_2", payload.GetProperty("nextPlayerId").GetString());
        Assert.Equal(beforeEnd.TurnNumber + 1, payload.GetProperty("turnIndex").GetInt32());
        Assert.Equal("player_2", afterEnd.CurrentTurnPlayerId?.Value);
        Assert.Equal(beforeEnd.TurnNumber + 1, afterEnd.TurnNumber);
        Assert.False(afterEnd.HasRolledThisTurn);
        Assert.False(afterEnd.HasResolvedTileThisTurn);
        Assert.False(afterEnd.HasExecutedTileThisTurn);
        Assert.Null(afterEnd.ActiveAuctionState);
        Assert.Equal(beforeEnd.Phase, afterEnd.Phase);
    }

    [Fact]
    public void EndTurn_BeforeRollReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(3, 3));
        var started = StartReadyGame(sessionManager, handler);

        using var response = Handle(
            handler,
            started.FirstContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void EndTurn_BeforeResolveReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(3, 3));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);

        using var response = Handle(
            handler,
            started.FirstContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void EndTurn_BeforeExecuteReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(3, 3));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);
        _ = handler.HandleTextMessage(ResolveTileMessage(started.Session.SessionId, "player_1"), started.FirstContext);

        using var response = Handle(
            handler,
            started.FirstContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void EndTurn_LockedCurrentPlayerBeforeExecuteReturnsPlayerLocked()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(3, 3));
        var started = StartReadyGame(sessionManager, handler);
        _ = handler.HandleTextMessage(RollDiceMessage(started.Session.SessionId, "player_1"), started.FirstContext);
        _ = handler.HandleTextMessage(ResolveTileMessage(started.Session.SessionId, "player_1"), started.FirstContext);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            player => player with { IsLockedUp = true });
        var beforeEnd = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));
        var afterEnd = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        AssertError(response, "player_locked");
        Assert.Same(beforeEnd, afterEnd);
    }

    [Fact]
    public void EndTurn_ActiveAuctionReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            "property_01");
        _ = handler.HandleTextMessage(ExecuteTileMessage(started.Session.SessionId, "player_1"), started.FirstContext);

        using var response = Handle(
            handler,
            started.FirstContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void PlaceBid_ValidFirstBidReturnsResultAndUpdatesActiveAuctionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var session = StartActiveAuction(sessionManager, started.Session.SessionId);
        var beforeBid = session.GameState;

        using var response = Handle(
            handler,
            started.SecondContext,
            PlaceBidMessage(started.Session.SessionId, "player_2", 25));
        var payload = AssertResponseType(response, "bid_result");
        var afterBid = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("player_2", payload.GetProperty("bidderPlayerId").GetString());
        Assert.Equal(25, payload.GetProperty("amount").GetInt32());
        Assert.Equal(25, payload.GetProperty("currentHighestBid").GetInt32());
        Assert.Equal("player_2", payload.GetProperty("highestBidderId").GetString());
        Assert.Equal(AuctionStatus.ActiveBidCountdown, afterBid.ActiveAuctionState?.Status);
        Assert.Equal(new Money(25), afterBid.ActiveAuctionState?.HighestBid);
        Assert.Equal(new PlayerId("player_2"), afterBid.ActiveAuctionState?.HighestBidderId);
        Assert.Equal(3, afterBid.ActiveAuctionState?.CountdownDurationSeconds);
        Assert.Single(afterBid.ActiveAuctionState?.Bids ?? Array.Empty<AuctionBid>());
        Assert.Equal(beforeBid.Phase, afterBid.Phase);
    }

    [Fact]
    public void PlaceBid_NonCurrentPlayerCanPlaceHigherBid()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        _ = handler.HandleTextMessage(PlaceBidMessage(started.Session.SessionId, "player_1", 10), started.FirstContext);

        using var response = Handle(
            handler,
            started.SecondContext,
            PlaceBidMessage(started.Session.SessionId, "player_2", 11));
        var payload = AssertResponseType(response, "bid_result");
        var afterBid = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("player_2", payload.GetProperty("bidderPlayerId").GetString());
        Assert.Equal(11, payload.GetProperty("currentHighestBid").GetInt32());
        Assert.Equal("player_2", payload.GetProperty("highestBidderId").GetString());
        Assert.Equal(new PlayerId("player_2"), afterBid.ActiveAuctionState?.HighestBidderId);
        Assert.Equal(2, afterBid.ActiveAuctionState?.Bids.Count);
    }

    [Fact]
    public void PlaceBid_TooLowBidReturnsBidTooLowAndPreservesAuctionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(
            sessionManager,
            started.Session.SessionId,
            AuctionConfig.Default with { MinimumBidIncrement = new Money(5) });
        _ = handler.HandleTextMessage(PlaceBidMessage(started.Session.SessionId, "player_1", 10), started.FirstContext);
        var beforeRejectedBid = sessionManager.GetSession(started.Session.SessionId)!.GameState.ActiveAuctionState;

        using var response = Handle(
            handler,
            started.SecondContext,
            PlaceBidMessage(started.Session.SessionId, "player_2", 14));
        var afterRejectedBid = sessionManager.GetSession(started.Session.SessionId)!.GameState.ActiveAuctionState;

        AssertError(response, "bid_too_low");
        Assert.Same(beforeRejectedBid, afterRejectedBid);
        Assert.Equal(new Money(10), afterRejectedBid?.HighestBid);
        Assert.Equal(new PlayerId("player_1"), afterRejectedBid?.HighestBidderId);
    }

    [Fact]
    public void PlaceBid_EqualBidReturnsBidTooLowAndPreservesAuctionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        _ = handler.HandleTextMessage(PlaceBidMessage(started.Session.SessionId, "player_1", 10), started.FirstContext);
        var beforeRejectedBid = sessionManager.GetSession(started.Session.SessionId)!.GameState.ActiveAuctionState;

        using var response = Handle(
            handler,
            started.SecondContext,
            PlaceBidMessage(started.Session.SessionId, "player_2", 10));
        var afterRejectedBid = sessionManager.GetSession(started.Session.SessionId)!.GameState.ActiveAuctionState;

        AssertError(response, "bid_too_low");
        Assert.Same(beforeRejectedBid, afterRejectedBid);
        Assert.Equal(new Money(10), afterRejectedBid?.HighestBid);
        Assert.Equal(new PlayerId("player_1"), afterRejectedBid?.HighestBidderId);
    }

    [Fact]
    public void PlaceBid_EliminatedPlayerReturnsPlayerEliminated()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_2",
            player => player with { IsEliminated = true });

        using var response = Handle(
            handler,
            started.SecondContext,
            PlaceBidMessage(started.Session.SessionId, "player_2", 10));

        AssertError(response, "player_eliminated");
    }

    [Fact]
    public void PlaceBid_LockedPlayerCanBidDuringActiveAuction()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_2",
            player => player with { IsLockedUp = true });

        using var response = Handle(
            handler,
            started.SecondContext,
            PlaceBidMessage(started.Session.SessionId, "player_2", 10));
        var payload = AssertResponseType(response, "bid_result");
        var afterBid = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("player_2", payload.GetProperty("highestBidderId").GetString());
        Assert.Equal(new PlayerId("player_2"), afterBid.ActiveAuctionState?.HighestBidderId);
    }

    [Fact]
    public void PlaceBid_NoActiveAuctionReturnsAuctionNotActive()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);

        using var response = Handle(
            handler,
            started.FirstContext,
            PlaceBidMessage(started.Session.SessionId, "player_1", 10));

        AssertError(response, "auction_not_active");
    }

    [Fact]
    public void PlaceBid_InactiveAuctionStatusReturnsAuctionNotActive()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var session = StartActiveAuction(sessionManager, started.Session.SessionId);
        var inactiveAuction = session.GameState.ActiveAuctionState! with
        {
            Status = (AuctionStatus)999,
        };
        _ = UpdateGameState(
            sessionManager,
            started.Session.SessionId,
            gameState => gameState with { ActiveAuctionState = inactiveAuction });

        using var response = Handle(
            handler,
            started.FirstContext,
            PlaceBidMessage(started.Session.SessionId, "player_1", 10));

        AssertError(response, "auction_not_active");
    }

    [Fact]
    public void PlaceBid_MultiplePlayersBiddingSequenceUpdatesHighestBidder()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);

        _ = handler.HandleTextMessage(PlaceBidMessage(started.Session.SessionId, "player_1", 10), started.FirstContext);
        _ = handler.HandleTextMessage(PlaceBidMessage(started.Session.SessionId, "player_2", 11), started.SecondContext);
        using var response = Handle(
            handler,
            started.FirstContext,
            PlaceBidMessage(started.Session.SessionId, "player_1", 12));
        var payload = AssertResponseType(response, "bid_result");
        var afterBid = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("player_1", payload.GetProperty("highestBidderId").GetString());
        Assert.Equal(12, payload.GetProperty("currentHighestBid").GetInt32());
        Assert.Equal(new PlayerId("player_1"), afterBid.ActiveAuctionState?.HighestBidderId);
        Assert.Equal(new Money(12), afterBid.ActiveAuctionState?.HighestBid);
        Assert.Equal(3, afterBid.ActiveAuctionState?.Bids.Count);
    }

    [Theory]
    [InlineData(@"{""type"":""place_bid"",""payload"":{""playerId"":""player_1"",""amount"":10}}")]
    [InlineData(@"{""type"":""place_bid"",""payload"":{""sessionId"":""session_1"",""amount"":10}}")]
    [InlineData(@"{""type"":""place_bid"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""place_bid"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""amount"":0}}")]
    [InlineData(@"{""type"":""place_bid"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""amount"":-1}}")]
    [InlineData(@"{""type"":""place_bid"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""amount"":""10""}}")]
    [InlineData(@"{""type"":""place_bid"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""amount"":10.5}}")]
    public void PlaceBid_InvalidPayloadReturnsInvalidPayload(string message)
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, message);

        AssertError(response, "invalid_payload");
    }

    [Fact]
    public void PlaceBid_WrongPlayerContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);

        using var response = Handle(
            handler,
            started.SecondContext,
            PlaceBidMessage(started.Session.SessionId, "player_1", 10));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void TakeLoan_ValidCurrentPlayerRentPaymentReturnsLoanResultAndUpdatesState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);

        using var response = Handle(
            handler,
            started.FirstContext,
            TakeLoanMessage(started.Session.SessionId, "player_1", 200, "rent_payment"));
        var payload = AssertResponseType(response, "loan_result");
        var player = sessionManager.GetSession(started.Session.SessionId)!.GameState.Players[0];

        Assert.Equal("player_1", payload.GetProperty("playerId").GetString());
        Assert.Equal(200, payload.GetProperty("amount").GetInt32());
        Assert.Equal("rent_payment", payload.GetProperty("reason").GetString());
        Assert.Equal(1700, payload.GetProperty("money").GetInt32());
        Assert.Equal(200, payload.GetProperty("totalBorrowed").GetInt32());
        Assert.Equal(20, payload.GetProperty("currentInterestRatePercent").GetInt32());
        Assert.Equal(40, payload.GetProperty("nextTurnInterestDue").GetInt32());
        Assert.Equal(1, payload.GetProperty("loanTier").GetInt32());
        Assert.Equal(new Money(1700), player.Money);
        Assert.Equal(new Money(200), player.LoanState?.TotalBorrowed);
    }

    [Fact]
    public void TakeLoan_ConsecutiveLoansAccumulateFromLatestPersistedState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);

        _ = handler.HandleTextMessage(
            TakeLoanMessage(started.Session.SessionId, "player_1", 125, "rent_payment"),
            started.FirstContext);
        using var response = Handle(
            handler,
            started.FirstContext,
            TakeLoanMessage(started.Session.SessionId, "player_1", 75, "fine"));
        var payload = AssertResponseType(response, "loan_result");
        var player = sessionManager.GetSession(started.Session.SessionId)!.GameState.Players[0];

        Assert.Equal(1700, payload.GetProperty("money").GetInt32());
        Assert.Equal(200, payload.GetProperty("totalBorrowed").GetInt32());
        Assert.Equal(30, payload.GetProperty("currentInterestRatePercent").GetInt32());
        Assert.Equal(60, payload.GetProperty("nextTurnInterestDue").GetInt32());
        Assert.Equal(2, payload.GetProperty("loanTier").GetInt32());
        Assert.Equal(new Money(1700), player.Money);
        Assert.Equal(new Money(200), player.LoanState?.TotalBorrowed);
        Assert.Equal(2, player.LoanState?.LoanTier);
    }

    [Fact]
    public void TakeLoan_NonCurrentAuctionBidDuringActiveAuctionReturnsLoanResult()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);

        using var response = Handle(
            handler,
            started.SecondContext,
            TakeLoanMessage(started.Session.SessionId, "player_2", 200, "auction_bid"));
        var payload = AssertResponseType(response, "loan_result");
        var player = sessionManager.GetSession(started.Session.SessionId)!.GameState.Players[1];

        Assert.Equal("player_2", payload.GetProperty("playerId").GetString());
        Assert.Equal("auction_bid", payload.GetProperty("reason").GetString());
        Assert.Equal(1700, payload.GetProperty("money").GetInt32());
        Assert.Equal(new Money(1700), player.Money);
    }

    [Fact]
    public void TakeLoan_LockedPlayerAuctionBidDuringActiveAuctionReturnsLoanResult()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_2",
            player => player with { IsLockedUp = true });

        using var response = Handle(
            handler,
            started.SecondContext,
            TakeLoanMessage(started.Session.SessionId, "player_2", 200, "auction_bid"));
        var payload = AssertResponseType(response, "loan_result");
        var player = sessionManager.GetSession(started.Session.SessionId)!.GameState.Players[1];

        Assert.Equal("player_2", payload.GetProperty("playerId").GetString());
        Assert.Equal("auction_bid", payload.GetProperty("reason").GetString());
        Assert.Equal(new Money(1700), player.Money);
    }

    [Fact]
    public void TakeLoan_AuctionBidOutsideActiveAuctionReturnsAuctionNotActive()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);

        using var response = Handle(
            handler,
            started.FirstContext,
            TakeLoanMessage(started.Session.SessionId, "player_1", 200, "auction_bid"));

        AssertError(response, "auction_not_active");
    }

    [Fact]
    public void TakeLoan_NonAuctionReasonDuringActiveAuctionReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);

        using var response = Handle(
            handler,
            started.FirstContext,
            TakeLoanMessage(started.Session.SessionId, "player_1", 200, "rent_payment"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void TakeLoan_DisabledLoanModeReturnsLoanModeDisabledAndPreservesState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = UpdateGameState(
            sessionManager,
            started.Session.SessionId,
            gameState => gameState with { LoanSharkConfig = new LoanSharkConfig { Enabled = false } });
        var beforeLoan = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            TakeLoanMessage(started.Session.SessionId, "player_1", 200, "rent_payment"));
        var afterLoan = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        AssertError(response, "loan_mode_disabled");
        Assert.Same(beforeLoan, afterLoan);
        Assert.Equal(new Money(1500), afterLoan.Players[0].Money);
        Assert.Null(afterLoan.Players[0].LoanState);
    }

    [Theory]
    [InlineData("loan_interest")]
    [InlineData("loan_principal_repayment")]
    [InlineData("existing_loan_debt")]
    public void TakeLoan_BlockedReasonsReturnLoanReasonBlockedAndPreserveState(string reason)
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var beforeLoan = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            TakeLoanMessage(started.Session.SessionId, "player_1", 200, reason));
        var afterLoan = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        AssertError(response, "loan_reason_blocked");
        Assert.Same(beforeLoan, afterLoan);
        Assert.Null(afterLoan.Players[0].LoanState);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void TakeLoan_InvalidAmountReturnsInvalidLoanAmount(int amount)
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);

        using var response = Handle(
            handler,
            started.FirstContext,
            TakeLoanMessage(started.Session.SessionId, "player_1", amount, "rent_payment"));

        AssertError(response, "invalid_loan_amount");
    }

    [Theory]
    [InlineData(@"{""type"":""take_loan"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""amount"":10}}")]
    [InlineData(@"{""type"":""take_loan"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""amount"":10,""reason"":true}}")]
    [InlineData(@"{""type"":""take_loan"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""amount"":10,""reason"":""RentPayment""}}")]
    [InlineData(@"{""type"":""take_loan"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""amount"":10,""reason"":""rentPayment""}}")]
    [InlineData(@"{""type"":""take_loan"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""amount"":10,""reason"":""0""}}")]
    [InlineData(@"{""type"":""take_loan"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""amount"":""10"",""reason"":""rent_payment""}}")]
    [InlineData(@"{""type"":""take_loan"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""amount"":10.5,""reason"":""rent_payment""}}")]
    public void TakeLoan_InvalidPayloadReturnsInvalidPayload(string message)
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, message);

        AssertError(response, "invalid_payload");
    }

    [Fact]
    public void TakeLoan_PlayerMissingFromGameStateReturnsPlayerNotFound()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = UpdateGameState(
            sessionManager,
            started.Session.SessionId,
            gameState => gameState with
            {
                Players = gameState.Players
                    .Where(player => player.PlayerId.Value != "player_1")
                    .ToArray(),
            });

        using var response = Handle(
            handler,
            started.FirstContext,
            TakeLoanMessage(started.Session.SessionId, "player_1", 200, "rent_payment"));

        AssertError(response, "player_not_found");
    }

    [Fact]
    public void TakeLoan_InvalidSessionReturnsInvalidSession()
    {
        var handler = CreateHandler(new SessionManager(), new DiceRoll(1, 2));
        var context = new LobbyConnectionContext("connection_1");
        context.Bind("missing_session", "player_1");

        using var response = Handle(
            handler,
            context,
            TakeLoanMessage("missing_session", "player_1", 200, "rent_payment"));

        AssertError(response, "invalid_session");
    }

    [Fact]
    public void TakeLoan_LobbySessionReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(
            handler,
            context,
            TakeLoanMessage(session.SessionId, "player_1", 200, "rent_payment"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void TakeLoan_EliminatedPlayerReturnsPlayerEliminated()
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
            TakeLoanMessage(started.Session.SessionId, "player_1", 200, "rent_payment"));

        AssertError(response, "player_eliminated");
    }

    [Fact]
    public void TakeLoan_WrongPlayerContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);

        using var response = Handle(
            handler,
            started.SecondContext,
            TakeLoanMessage(started.Session.SessionId, "player_1", 200, "rent_payment"));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void TakeLoan_NonCurrentNonAuctionBorrowReturnsNotYourTurn()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);

        using var response = Handle(
            handler,
            started.SecondContext,
            TakeLoanMessage(started.Session.SessionId, "player_2", 200, "rent_payment"));

        AssertError(response, "not_your_turn");
    }

    [Fact]
    public void TakeLoan_LockedCurrentPlayerNonAuctionBorrowOutsideAuctionReturnsPlayerLocked()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            player => player with { IsLockedUp = true });
        var beforeLoan = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            TakeLoanMessage(started.Session.SessionId, "player_1", 200, "rent_payment"));
        var afterLoan = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        AssertError(response, "player_locked");
        Assert.Same(beforeLoan, afterLoan);
        Assert.Null(afterLoan.Players[0].LoanState);
    }

    [Fact]
    public void TakeLoan_SuccessPreservesGamePhaseAndActiveAuctionState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        var beforeLoan = sessionManager.GetSession(started.Session.SessionId)!.GameState;
        var beforeAuction = beforeLoan.ActiveAuctionState;

        using var response = Handle(
            handler,
            started.SecondContext,
            TakeLoanMessage(started.Session.SessionId, "player_2", 200, "auction_bid"));
        var afterLoan = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        AssertResponseType(response, "loan_result");
        Assert.Equal(beforeLoan.Phase, afterLoan.Phase);
        Assert.Same(beforeAuction, afterLoan.ActiveAuctionState);
    }

    [Fact]
    public void GetSnapshot_ValidRequestReturnsDeterministicCopiedState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var placedAtUtc = new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero);
        _ = UpdateGameState(
            sessionManager,
            started.Session.SessionId,
            gameState =>
            {
                var updatedPlayers = gameState.Players
                    .Select(player => player.PlayerId.Value == "player_1"
                        ? player with
                        {
                            Money = new Money(1234),
                            CurrentTileId = new TileId("property_01"),
                            OwnedPropertyIds = new HashSet<TileId>
                            {
                                new("property_02"),
                                new("property_01"),
                            },
                            HeldCardIds = new HashSet<CardId>
                            {
                                new("Z_HELD"),
                                new("A_HELD"),
                            },
                            LoanState = new PlayerLoanState(
                                new Money(300),
                                CurrentInterestRatePercent: 15,
                                new Money(45),
                                LoanTier: 2),
                            IsLockedUp = true,
                        }
                        : player)
                    .ToArray();
                var deckStates = new Dictionary<string, CardDeckState>
                {
                    [CardDeckIds.Table] = new(
                        CardDeckIds.Table,
                        new[] { CreateCard("TABLE_02", CardActionKind.Unspecified) },
                        new[] { CreateCard("TABLE_01", CardActionKind.Unspecified) }),
                    [CardDeckIds.Chance] = new(
                        CardDeckIds.Chance,
                        new[] { CreateCard("CHANCE_02", CardActionKind.Unspecified) },
                        new[] { CreateCard("CHANCE_01", CardActionKind.Unspecified) }),
                };

                return gameState with
                {
                    Players = updatedPlayers,
                    TurnNumber = 3,
                    HasRolledThisTurn = true,
                    HasResolvedTileThisTurn = true,
                    HasExecutedTileThisTurn = false,
                    ActiveAuctionState = new AuctionState(
                        new TileId("property_01"),
                        new PlayerId("player_1"),
                        AuctionStatus.ActiveBidCountdown,
                        new Money(10),
                        new Money(1),
                        InitialPreBidSeconds: 9,
                        BidResetSeconds: 3,
                        new[] { new AuctionBid(new PlayerId("player_2"), new Money(20), placedAtUtc) },
                        new Money(20),
                        new PlayerId("player_2"),
                        CountdownDurationSeconds: 3),
                    CardDeckStates = deckStates,
                };
            });

        using var response = Handle(
            handler,
            started.FirstContext,
            GetSnapshotMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "snapshot_result");
        var turn = payload.GetProperty("turn");
        var players = payload.GetProperty("players").EnumerateArray().ToArray();
        var firstPlayer = players[0];
        var propertyTile = payload
            .GetProperty("board")
            .GetProperty("tiles")
            .EnumerateArray()
            .Single(tile => tile.GetProperty("tileId").GetString() == "property_01");
        var auction = payload.GetProperty("activeAuction");
        var bid = Assert.Single(auction.GetProperty("bids").EnumerateArray());
        var decks = payload.GetProperty("cardDecks").EnumerateArray().ToArray();

        Assert.Equal(1, payload.GetProperty("snapshotVersion").GetInt32());
        Assert.Equal(started.Session.SessionId, payload.GetProperty("sessionId").GetString());
        Assert.Equal("in_game", payload.GetProperty("status").GetString());
        Assert.Equal(started.Session.SessionId, payload.GetProperty("matchId").GetString());
        Assert.Equal("awaiting_roll", payload.GetProperty("phase").GetString());
        Assert.Equal("player_1", turn.GetProperty("currentPlayerId").GetString());
        Assert.Equal(3, turn.GetProperty("turnIndex").GetInt32());
        Assert.True(turn.GetProperty("hasRolledThisTurn").GetBoolean());
        Assert.True(turn.GetProperty("hasResolvedTileThisTurn").GetBoolean());
        Assert.False(turn.GetProperty("hasExecutedTileThisTurn").GetBoolean());
        Assert.Equal(new[] { "player_1", "player_2" }, players.Select(player => player.GetProperty("playerId").GetString()).ToArray());
        Assert.Equal(new[] { "property_01", "property_02" }, firstPlayer.GetProperty("ownedPropertyIds").EnumerateArray().Select(id => id.GetString()).ToArray());
        Assert.Equal(new[] { "A_HELD", "Z_HELD" }, firstPlayer.GetProperty("heldCardIds").EnumerateArray().Select(id => id.GetString()).ToArray());
        Assert.Equal(1234, firstPlayer.GetProperty("money").GetInt32());
        Assert.True(firstPlayer.GetProperty("isLockedUp").GetBoolean());
        Assert.Equal(300, firstPlayer.GetProperty("loan").GetProperty("totalBorrowed").GetInt32());
        Assert.Equal(15, firstPlayer.GetProperty("loan").GetProperty("currentInterestRatePercent").GetInt32());
        Assert.Equal(45, firstPlayer.GetProperty("loan").GetProperty("nextTurnInterestDue").GetInt32());
        Assert.Equal(2, firstPlayer.GetProperty("loan").GetProperty("loanTier").GetInt32());
        Assert.Equal("player_1", propertyTile.GetProperty("ownerPlayerId").GetString());
        Assert.Equal(new[] { 2, 10, 30, 90, 160, 250 }, propertyTile.GetProperty("rentTable").EnumerateArray().Select(rent => rent.GetInt32()).ToArray());
        Assert.Equal("active_bid_countdown", auction.GetProperty("status").GetString());
        Assert.Equal(20, auction.GetProperty("highestBid").GetInt32());
        Assert.Equal("player_2", auction.GetProperty("highestBidderId").GetString());
        Assert.Equal("player_2", bid.GetProperty("bidderPlayerId").GetString());
        Assert.Equal(20, bid.GetProperty("amount").GetInt32());
        Assert.Equal(new[] { "chance", "table" }, decks.Select(deck => deck.GetProperty("deckId").GetString()).ToArray());
        Assert.Equal("CHANCE_02", Assert.Single(decks[0].GetProperty("drawPileCardIds").EnumerateArray()).GetString());
        Assert.True(payload.GetProperty("loanShark").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void GetSnapshot_NoActiveAuctionReturnsNullAndDoesNotMutateGameState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var beforeSnapshot = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        using var response = Handle(
            handler,
            started.FirstContext,
            GetSnapshotMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "snapshot_result");
        var afterSnapshot = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal(JsonValueKind.Null, payload.GetProperty("activeAuction").ValueKind);
        Assert.Same(beforeSnapshot, afterSnapshot);
    }

    [Fact]
    public void GetSnapshot_AfterBidAndLoanReflectsPersistedState()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        _ = handler.HandleTextMessage(PlaceBidMessage(started.Session.SessionId, "player_2", 20), started.SecondContext);
        _ = handler.HandleTextMessage(TakeLoanMessage(started.Session.SessionId, "player_2", 200, "auction_bid"), started.SecondContext);

        using var response = Handle(
            handler,
            started.SecondContext,
            GetSnapshotMessage(started.Session.SessionId, "player_2"));
        var payload = AssertResponseType(response, "snapshot_result");
        var secondPlayer = payload
            .GetProperty("players")
            .EnumerateArray()
            .Single(player => player.GetProperty("playerId").GetString() == "player_2");

        Assert.Equal(20, payload.GetProperty("activeAuction").GetProperty("highestBid").GetInt32());
        Assert.Equal("player_2", payload.GetProperty("activeAuction").GetProperty("highestBidderId").GetString());
        Assert.Equal(1700, secondPlayer.GetProperty("money").GetInt32());
        Assert.Equal(200, secondPlayer.GetProperty("loan").GetProperty("totalBorrowed").GetInt32());
    }

    [Fact]
    public void GetSnapshot_InvalidSessionReturnsInvalidSession()
    {
        var handler = CreateHandler(new SessionManager(), new DiceRoll(1, 2));
        var context = new LobbyConnectionContext("connection_1");
        context.Bind("missing_session", "player_1");

        using var response = Handle(handler, context, GetSnapshotMessage("missing_session", "player_1"));

        AssertError(response, "invalid_session");
    }

    [Fact]
    public void GetSnapshot_BindingMismatchReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var unboundContext = new LobbyConnectionContext("connection_3");
        var wrongSessionContext = new LobbyConnectionContext("connection_4");
        wrongSessionContext.Bind("wrong_session", "player_1");

        using var unboundResponse = Handle(
            handler,
            unboundContext,
            GetSnapshotMessage(started.Session.SessionId, "player_1"));
        using var wrongPlayerResponse = Handle(
            handler,
            started.SecondContext,
            GetSnapshotMessage(started.Session.SessionId, "player_1"));
        using var wrongSessionResponse = Handle(
            handler,
            wrongSessionContext,
            GetSnapshotMessage(started.Session.SessionId, "player_1"));

        AssertError(unboundResponse, "player_switch_rejected");
        AssertError(wrongPlayerResponse, "player_switch_rejected");
        AssertError(wrongSessionResponse, "player_switch_rejected");
    }

    [Fact]
    public void GetSnapshot_PlayerMissingFromGameStateReturnsPlayerNotFound()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = UpdateGameState(
            sessionManager,
            started.Session.SessionId,
            gameState => gameState with
            {
                Players = gameState.Players
                    .Where(player => player.PlayerId.Value != "player_1")
                    .ToArray(),
            });

        using var response = Handle(
            handler,
            started.FirstContext,
            GetSnapshotMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_not_found");
    }

    [Fact]
    public void FinalizeAuction_ValidWinnerReturnsAuctionResultAndClearsActiveAuction()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var session = StartActiveAuction(sessionManager, started.Session.SessionId);
        var beforeFinalize = session.GameState;
        _ = handler.HandleTextMessage(PlaceBidMessage(started.Session.SessionId, "player_2", 260), started.SecondContext);

        using var response = Handle(
            handler,
            started.FirstContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "auction_result");
        var afterFinalize = sessionManager.GetSession(started.Session.SessionId)!.GameState;
        var winner = afterFinalize.Players.Single(player => player.PlayerId.Value == "player_2");

        Assert.Equal("won", payload.GetProperty("resultType").GetString());
        Assert.Equal("player_2", payload.GetProperty("winnerPlayerId").GetString());
        Assert.Equal(260, payload.GetProperty("amount").GetInt32());
        Assert.Equal("property_01", payload.GetProperty("tileId").GetString());
        Assert.Equal(1240, winner.Money.Amount);
        Assert.Contains(new TileId("property_01"), winner.OwnedPropertyIds);
        Assert.Null(afterFinalize.ActiveAuctionState);
        Assert.Equal(beforeFinalize.Phase, afterFinalize.Phase);
    }

    [Fact]
    public void FinalizeAuction_LockedCurrentPlayerCanFinalizeActiveAuction()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            player => player with { IsLockedUp = true });

        using var response = Handle(
            handler,
            started.FirstContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "auction_result");
        var afterFinalize = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("no_sale", payload.GetProperty("resultType").GetString());
        Assert.Null(afterFinalize.ActiveAuctionState);
    }

    [Fact]
    public void FinalizeAuction_WinnerFailedPaymentReturnsFailedPaymentAndClearsActiveAuction()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        _ = handler.HandleTextMessage(PlaceBidMessage(started.Session.SessionId, "player_2", 260), started.SecondContext);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_2",
            player => player with { Money = new Money(100) });

        using var response = Handle(
            handler,
            started.FirstContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "auction_result");
        var afterFinalize = sessionManager.GetSession(started.Session.SessionId)!.GameState;
        var failedWinner = afterFinalize.Players.Single(player => player.PlayerId.Value == "player_2");

        Assert.Equal("failed_payment", payload.GetProperty("resultType").GetString());
        Assert.Equal("player_2", payload.GetProperty("winnerPlayerId").GetString());
        Assert.Equal(260, payload.GetProperty("amount").GetInt32());
        Assert.Equal("property_01", payload.GetProperty("tileId").GetString());
        Assert.True(failedWinner.IsEliminated);
        Assert.True(failedWinner.IsBankrupt);
        Assert.Equal(100, failedWinner.Money.Amount);
        Assert.DoesNotContain(afterFinalize.Players, player => player.OwnedPropertyIds.Contains(new TileId("property_01")));
        Assert.Null(afterFinalize.ActiveAuctionState);
    }

    [Fact]
    public void FinalizeAuction_NoBidsReturnsNoSaleAndClearsActiveAuction()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);

        using var response = Handle(
            handler,
            started.FirstContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "auction_result");
        var afterFinalize = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("no_sale", payload.GetProperty("resultType").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("winnerPlayerId").ValueKind);
        Assert.Equal(0, payload.GetProperty("amount").GetInt32());
        Assert.Equal("property_01", payload.GetProperty("tileId").GetString());
        Assert.DoesNotContain(afterFinalize.Players, player => player.OwnedPropertyIds.Contains(new TileId("property_01")));
        Assert.Null(afterFinalize.ActiveAuctionState);
    }

    [Fact]
    public void FinalizeAuction_EliminatedHighestBidderAwardsHighestEligibleBidder()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        _ = handler.HandleTextMessage(PlaceBidMessage(started.Session.SessionId, "player_1", 10), started.FirstContext);
        _ = handler.HandleTextMessage(PlaceBidMessage(started.Session.SessionId, "player_2", 20), started.SecondContext);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_2",
            player => player with { IsEliminated = true });

        using var response = Handle(
            handler,
            started.FirstContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "auction_result");
        var afterFinalize = sessionManager.GetSession(started.Session.SessionId)!.GameState;
        var winner = afterFinalize.Players.Single(player => player.PlayerId.Value == "player_1");

        Assert.Equal("won", payload.GetProperty("resultType").GetString());
        Assert.Equal("player_1", payload.GetProperty("winnerPlayerId").GetString());
        Assert.Equal(10, payload.GetProperty("amount").GetInt32());
        Assert.Equal(1490, winner.Money.Amount);
        Assert.Contains(new TileId("property_01"), winner.OwnedPropertyIds);
        Assert.Null(afterFinalize.ActiveAuctionState);
    }

    [Fact]
    public void FinalizeAuction_AllBiddersEliminatedReturnsNoSale()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        _ = handler.HandleTextMessage(PlaceBidMessage(started.Session.SessionId, "player_2", 20), started.SecondContext);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_2",
            player => player with { IsEliminated = true });

        using var response = Handle(
            handler,
            started.FirstContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "auction_result");
        var afterFinalize = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("no_sale", payload.GetProperty("resultType").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("winnerPlayerId").ValueKind);
        Assert.Equal(0, payload.GetProperty("amount").GetInt32());
        Assert.DoesNotContain(afterFinalize.Players, player => player.OwnedPropertyIds.Contains(new TileId("property_01")));
        Assert.Null(afterFinalize.ActiveAuctionState);
    }

    [Fact]
    public void FinalizeAuction_NoActiveAuctionReturnsAuctionNotActive()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);

        using var response = Handle(
            handler,
            started.FirstContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "auction_not_active");
    }

    [Fact]
    public void FinalizeAuction_InactiveAuctionStatusReturnsAuctionNotActive()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var session = StartActiveAuction(sessionManager, started.Session.SessionId);
        var inactiveAuction = session.GameState.ActiveAuctionState! with
        {
            Status = (AuctionStatus)999,
        };
        _ = UpdateGameState(
            sessionManager,
            started.Session.SessionId,
            gameState => gameState with { ActiveAuctionState = inactiveAuction });

        using var response = Handle(
            handler,
            started.FirstContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "auction_not_active");
    }

    [Fact]
    public void FinalizeAuction_ManagerInvalidAuctionStateReturnsInvalidSessionStateAndPreservesAuction()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_2",
            player => player with { OwnedPropertyIds = new HashSet<TileId> { new("property_01") } });
        var beforeFinalize = sessionManager.GetSession(started.Session.SessionId)!.GameState.ActiveAuctionState;

        using var response = Handle(
            handler,
            started.FirstContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"));
        var afterFinalize = sessionManager.GetSession(started.Session.SessionId)!.GameState.ActiveAuctionState;

        AssertError(response, "invalid_session_state");
        Assert.Same(beforeFinalize, afterFinalize);
    }

    [Fact]
    public void FinalizeAuction_WrongPlayerContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);

        using var response = Handle(
            handler,
            started.SecondContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void FinalizeAuction_NonCurrentPlayerReturnsNotYourTurn()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);

        using var response = Handle(
            handler,
            started.SecondContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_2"));

        AssertError(response, "not_your_turn");
    }

    [Fact]
    public void FinalizeAuction_EliminatedCurrentPlayerReturnsPlayerEliminated()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = StartActiveAuction(sessionManager, started.Session.SessionId);
        _ = UpdateEnginePlayer(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            player => player with { IsEliminated = true });

        using var response = Handle(
            handler,
            started.FirstContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_eliminated");
    }

    [Fact]
    public void FinalizeAuction_LobbySessionReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(handler, context, FinalizeAuctionMessage(session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void FinalizeAuction_InvalidSessionReturnsInvalidSession()
    {
        var handler = CreateHandler(new SessionManager(), new DiceRoll(1, 2));
        var context = new LobbyConnectionContext("connection_1");
        context.Bind("missing_session", "player_1");

        using var response = Handle(handler, context, FinalizeAuctionMessage("missing_session", "player_1"));

        AssertError(response, "invalid_session");
    }

    [Fact]
    public void FinalizeAuction_PlayerMissingFromGameStateReturnsPlayerNotFound()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = UpdateGameState(
            sessionManager,
            started.Session.SessionId,
            gameState => gameState with
            {
                Players = gameState.Players
                    .Where(player => player.PlayerId.Value != "player_1")
                    .ToArray(),
            });

        using var response = Handle(
            handler,
            started.FirstContext,
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_not_found");
    }

    [Fact]
    public void EndTurn_AfterAuctionFinalizationClearsActiveAuctionCanAdvance()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            "property_01");
        _ = handler.HandleTextMessage(ExecuteTileMessage(started.Session.SessionId, "player_1"), started.FirstContext);
        _ = handler.HandleTextMessage(PlaceBidMessage(started.Session.SessionId, "player_2", 10), started.SecondContext);
        _ = handler.HandleTextMessage(
            FinalizeAuctionMessage(started.Session.SessionId, "player_1"),
            started.FirstContext);

        using var response = Handle(
            handler,
            started.FirstContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));
        var payload = AssertResponseType(response, "end_turn_result");
        var afterEnd = sessionManager.GetSession(started.Session.SessionId)!.GameState;

        Assert.Equal("player_2", payload.GetProperty("nextPlayerId").GetString());
        Assert.Null(afterEnd.ActiveAuctionState);
        Assert.Equal("player_2", afterEnd.CurrentTurnPlayerId?.Value);
    }

    [Fact]
    public void EndTurn_WrongPlayerContextReturnsPlayerSwitchRejected()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(3, 3));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_1", "start");

        using var response = Handle(
            handler,
            started.SecondContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_switch_rejected");
    }

    [Fact]
    public void EndTurn_NonCurrentPlayerReturnsNotYourTurn()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(3, 3));
        var started = StartReadyGame(sessionManager, handler);
        _ = SetCurrentPlayerReadyToExecuteTile(sessionManager, started.Session.SessionId, "player_2", "start");

        using var response = Handle(
            handler,
            started.SecondContext,
            EndTurnMessage(started.Session.SessionId, "player_2"));

        AssertError(response, "not_your_turn");
    }

    [Fact]
    public void EndTurn_EliminatedCurrentPlayerAfterExecuteReturnsPlayerEliminated()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(1, 2));
        var started = StartReadyGame(sessionManager, handler);
        var readySession = SetCurrentPlayerReadyToExecuteTile(
            sessionManager,
            started.Session.SessionId,
            "player_1",
            "property_01");
        _ = UpdateGameState(
            sessionManager,
            readySession.SessionId,
            gameState => gameState with
            {
                Players = gameState.Players
                    .Select(player => player.PlayerId.Value switch
                    {
                        "player_1" => player with { Money = new Money(1) },
                        "player_2" => player with { OwnedPropertyIds = new HashSet<TileId> { new("property_01") } },
                        _ => player,
                    })
                    .ToArray(),
            });
        _ = handler.HandleTextMessage(ExecuteTileMessage(started.Session.SessionId, "player_1"), started.FirstContext);

        using var response = Handle(
            handler,
            started.FirstContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_eliminated");
    }

    [Fact]
    public void EndTurn_InvalidSessionReturnsInvalidSession()
    {
        var handler = CreateHandler(new SessionManager(), new DiceRoll(3, 3));
        var context = new LobbyConnectionContext("connection_1");
        context.Bind("missing_session", "player_1");

        using var response = Handle(handler, context, EndTurnMessage("missing_session", "player_1"));

        AssertError(response, "invalid_session");
    }

    [Fact]
    public void EndTurn_LobbySessionReturnsInvalidSessionState()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = CreateHandler(sessionManager, new DiceRoll(3, 3));
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(handler, context, EndTurnMessage(session.SessionId, "player_1"));

        AssertError(response, "invalid_session_state");
    }

    [Fact]
    public void EndTurn_PlayerMissingFromGameStateReturnsPlayerNotFound()
    {
        var sessionManager = new SessionManager();
        var handler = CreateHandler(sessionManager, new DiceRoll(3, 3));
        var started = StartReadyGame(sessionManager, handler);
        _ = UpdateGameState(
            sessionManager,
            started.Session.SessionId,
            gameState => gameState with
            {
                Players = gameState.Players
                    .Where(player => player.PlayerId.Value != "player_1")
                    .ToArray(),
            });

        using var response = Handle(
            handler,
            started.FirstContext,
            EndTurnMessage(started.Session.SessionId, "player_1"));

        AssertError(response, "player_not_found");
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

    [Fact]
    public void ClientSentExecuteTileResultReturnsUnsupportedMessage()
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, @"{""type"":""execute_tile_result""}");

        AssertError(response, "unsupported_message");
    }

    [Fact]
    public void ClientSentEndTurnResultReturnsUnsupportedMessage()
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, @"{""type"":""end_turn_result""}");

        AssertError(response, "unsupported_message");
    }

    [Fact]
    public void ClientSentBidResultReturnsUnsupportedMessage()
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, @"{""type"":""bid_result""}");

        AssertError(response, "unsupported_message");
    }

    [Fact]
    public void ClientSentAuctionResultReturnsUnsupportedMessage()
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, @"{""type"":""auction_result""}");

        AssertError(response, "unsupported_message");
    }

    [Fact]
    public void ClientSentLoanResultReturnsUnsupportedMessage()
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, @"{""type"":""loan_result""}");

        AssertError(response, "unsupported_message");
    }

    [Fact]
    public void ClientSentSnapshotResultReturnsUnsupportedMessage()
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, @"{""type"":""snapshot_result""}");

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
    [InlineData(@"{""type"":""execute_tile"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""execute_tile"",""payload"":{""sessionId"":""session_1""}}")]
    [InlineData(@"{""type"":""end_turn"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""end_turn"",""payload"":{""sessionId"":""session_1""}}")]
    [InlineData(@"{""type"":""place_bid"",""payload"":{""playerId"":""player_1"",""amount"":10}}")]
    [InlineData(@"{""type"":""place_bid"",""payload"":{""sessionId"":""session_1"",""amount"":10}}")]
    [InlineData(@"{""type"":""finalize_auction"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""finalize_auction"",""payload"":{""sessionId"":""session_1""}}")]
    [InlineData(@"{""type"":""take_loan"",""payload"":{""playerId"":""player_1"",""amount"":10,""reason"":""rent_payment""}}")]
    [InlineData(@"{""type"":""take_loan"",""payload"":{""sessionId"":""session_1"",""amount"":10,""reason"":""rent_payment""}}")]
    [InlineData(@"{""type"":""get_snapshot"",""payload"":{""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""get_snapshot"",""payload"":{""sessionId"":""session_1""}}")]
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

    private static GameSession UpdateGameState(
        SessionManager sessionManager,
        string sessionId,
        Func<GameState, GameState> updateGameState)
    {
        var session = sessionManager.GetSession(sessionId)!;

        return sessionManager.UpdateGameState(sessionId, updateGameState(session.GameState));
    }

    private static GameSession UpdateDeckState(
        SessionManager sessionManager,
        string sessionId,
        string deckId,
        CardDeckState deckState)
    {
        return UpdateGameState(
            sessionManager,
            sessionId,
            gameState =>
            {
                var deckStates = new Dictionary<string, CardDeckState>(gameState.CardDeckStates)
                {
                    [deckId] = deckState,
                };

                return gameState with { CardDeckStates = deckStates };
            });
    }

    private static Card CreateCard(
        string cardId,
        CardActionKind actionKind,
        CardActionParameters? parameters = null)
    {
        return new Card(new CardId(cardId), cardId, actionKind, parameters);
    }

    private static GameSession SetCurrentPlayerReadyToExecuteTile(
        SessionManager sessionManager,
        string sessionId,
        string playerId,
        string tileId)
    {
        return UpdateGameState(
            sessionManager,
            sessionId,
            gameState =>
            {
                var updatedPlayers = gameState.Players
                    .Select(player => player.PlayerId.Value == playerId
                        ? player with { CurrentTileId = new TileId(tileId) }
                        : player)
                    .ToArray();

                return gameState with
                {
                    Players = updatedPlayers,
                    HasRolledThisTurn = true,
                    HasResolvedTileThisTurn = true,
                    HasExecutedTileThisTurn = false,
                    ActiveAuctionState = null,
                };
            });
    }

    private static GameSession StartActiveAuction(
        SessionManager sessionManager,
        string sessionId,
        AuctionConfig? config = null)
    {
        return UpdateGameState(
            sessionManager,
            sessionId,
            gameState =>
            {
                var auctionStart = AuctionManager.StartMandatoryAuction(
                    gameState,
                    new PlayerId("player_1"),
                    new TileId("property_01"),
                    config);

                return gameState with { ActiveAuctionState = auctionStart.AuctionState };
            });
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

    private static string ExecuteTileMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""execute_tile"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string EndTurnMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""end_turn"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string PlaceBidMessage(string sessionId, string playerId, int amount)
    {
        return $@"{{""type"":""place_bid"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}"",""amount"":{amount}}}}}";
    }

    private static string FinalizeAuctionMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""finalize_auction"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string TakeLoanMessage(string sessionId, string playerId, int amount, string reason)
    {
        return $@"{{""type"":""take_loan"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}"",""amount"":{amount},""reason"":""{reason}""}}}}";
    }

    private static string GetSnapshotMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""get_snapshot"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
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
