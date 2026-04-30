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

    [Theory]
    [InlineData("chance_01")]
    [InlineData("table_01")]
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
