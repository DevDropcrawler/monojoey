namespace MonoJoey.Server.Realtime;

using System.Text.Json;
using MonoJoey.Server.GameEngine;
using MonoJoey.Server.Sessions;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public sealed class LobbyMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object sessionLock = new();
    private readonly DiceService diceService;
    private readonly SessionManager sessionManager;

    public LobbyMessageHandler(SessionManager sessionManager)
        : this(sessionManager, new DiceService(new RandomDiceRoller()))
    {
    }

    public LobbyMessageHandler(SessionManager sessionManager, DiceService diceService)
    {
        this.sessionManager = sessionManager;
        this.diceService = diceService;
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
                if (session.Status == GameSessionStatus.Lobby)
                {
                    _ = sessionManager.LeaveSession(
                        connectionContext.SessionId,
                        new PlayerConnection(
                            new PlayerId(connectionContext.PlayerId),
                            connectionContext.ConnectionId,
                            IsReady: false));
                }
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
            LobbyMessageTypes.SetReady => HandleSetReady(root, connectionContext),
            LobbyMessageTypes.StartGame => HandleStartGame(root, connectionContext),
            LobbyMessageTypes.RollDice => HandleRollDice(root, connectionContext),
            LobbyMessageTypes.ResolveTile => HandleResolveTile(root, connectionContext),
            LobbyMessageTypes.ExecuteTile => HandleExecuteTile(root, connectionContext),
            LobbyMessageTypes.EndTurn => HandleEndTurn(root, connectionContext),
            LobbyMessageTypes.PlaceBid => HandlePlaceBid(root, connectionContext),
            LobbyMessageTypes.FinalizeAuction => HandleFinalizeAuction(root, connectionContext),
            LobbyMessageTypes.LobbyState or
                LobbyMessageTypes.GameStarted or
                LobbyMessageTypes.RollResult or
                LobbyMessageTypes.ResolveTileResult or
                LobbyMessageTypes.ExecuteTileResult or
                LobbyMessageTypes.EndTurnResult or
                LobbyMessageTypes.BidResult or
                LobbyMessageTypes.AuctionResult or
                LobbyMessageTypes.Error => CreateError(
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

    private LobbyServerEnvelope HandleRollDice(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadLobbyPlayerPayload(root, out var sessionId, out var playerId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "roll_dice requires payload.sessionId and payload.playerId.");
        }

        lock (sessionLock)
        {
            var session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSession,
                    "Session not found.");
            }

            if (!IsBoundToSessionPlayer(connectionContext, sessionId, playerId))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerSwitchRejected,
                    "This connection is not bound to that session and playerId.");
            }

            if (session.Status != GameSessionStatus.InGame)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Session is not in game.");
            }

            var player = session.GameState.Players.FirstOrDefault(
                gamePlayer => gamePlayer.PlayerId.Value == playerId);
            if (player is null)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotFound,
                    "Player is not in the game.");
            }

            if (session.GameState.CurrentTurnPlayerId?.Value != playerId)
            {
                return CreateError(
                    LobbyErrorCodes.NotYourTurn,
                    "It is not this player's turn.");
            }

            if (player.IsEliminated)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerEliminated,
                    "Eliminated players cannot roll dice.");
            }

            if (player.IsLockedUp)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerLocked,
                    "Locked players cannot roll dice.");
            }

            if (session.GameState.HasRolledThisTurn)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Player has already rolled this turn.");
            }

            var dice = diceService.RollDice();
            var movementResult = MovementManager.MovePlayer(
                session.GameState,
                player.PlayerId,
                dice.Total);
            var updatedGameState = movementResult.GameState with
            {
                HasRolledThisTurn = true,
                HasResolvedTileThisTurn = false,
                HasExecutedTileThisTurn = false,
            };

            _ = sessionManager.UpdateGameState(sessionId, updatedGameState);

            return CreateRollResult(dice, movementResult, updatedGameState.HasRolledThisTurn);
        }
    }

    private LobbyServerEnvelope HandleResolveTile(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadLobbyPlayerPayload(root, out var sessionId, out var playerId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "resolve_tile requires payload.sessionId and payload.playerId.");
        }

        lock (sessionLock)
        {
            var session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSession,
                    "Session not found.");
            }

            if (!IsBoundToSessionPlayer(connectionContext, sessionId, playerId))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerSwitchRejected,
                    "This connection is not bound to that session and playerId.");
            }

            if (session.Status != GameSessionStatus.InGame)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Session is not in game.");
            }

            var player = session.GameState.Players.FirstOrDefault(
                gamePlayer => gamePlayer.PlayerId.Value == playerId);
            if (player is null)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotFound,
                    "Player is not in the game.");
            }

            if (session.GameState.CurrentTurnPlayerId?.Value != playerId)
            {
                return CreateError(
                    LobbyErrorCodes.NotYourTurn,
                    "It is not this player's turn.");
            }

            if (player.IsEliminated)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerEliminated,
                    "Eliminated players cannot resolve tiles.");
            }

            if (player.IsLockedUp)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerLocked,
                    "Locked players cannot resolve tiles.");
            }

            if (!session.GameState.HasRolledThisTurn)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Player must roll before resolving a tile.");
            }

            if (session.GameState.HasResolvedTileThisTurn)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Player has already resolved a tile this turn.");
            }

            var resolution = TileResolver.ResolveCurrentTile(session.GameState, player.PlayerId);
            var updatedGameState = session.GameState with
            {
                HasResolvedTileThisTurn = true,
            };

            _ = sessionManager.UpdateGameState(sessionId, updatedGameState);

            return CreateResolveTileResult(resolution);
        }
    }

    private LobbyServerEnvelope HandleExecuteTile(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadLobbyPlayerPayload(root, out var sessionId, out var playerId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "execute_tile requires payload.sessionId and payload.playerId.");
        }

        lock (sessionLock)
        {
            var session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSession,
                    "Session not found.");
            }

            if (!IsBoundToSessionPlayer(connectionContext, sessionId, playerId))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerSwitchRejected,
                    "This connection is not bound to that session and playerId.");
            }

            if (session.Status != GameSessionStatus.InGame)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Session is not in game.");
            }

            var player = session.GameState.Players.FirstOrDefault(
                gamePlayer => gamePlayer.PlayerId.Value == playerId);
            if (player is null)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotFound,
                    "Player is not in the game.");
            }

            if (session.GameState.CurrentTurnPlayerId?.Value != playerId)
            {
                return CreateError(
                    LobbyErrorCodes.NotYourTurn,
                    "It is not this player's turn.");
            }

            if (player.IsEliminated)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerEliminated,
                    "Eliminated players cannot execute tile effects.");
            }

            if (player.IsLockedUp)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerLocked,
                    "Locked players cannot execute tile effects.");
            }

            if (!session.GameState.HasRolledThisTurn)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Player must roll before executing a tile.");
            }

            if (!session.GameState.HasResolvedTileThisTurn)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Player must resolve a tile before executing it.");
            }

            if (session.GameState.HasExecutedTileThisTurn)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Player has already executed a tile this turn.");
            }

            if (session.GameState.ActiveAuctionState is not null)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "An auction is already active.");
            }

            var resolution = TileResolver.ResolveCurrentTile(session.GameState, player.PlayerId);
            return resolution.ActionKind switch
            {
                TileResolutionActionKind.NoAction or
                    TileResolutionActionKind.StartPlaceholder => ExecuteNoActionTile(sessionId, session.GameState, resolution),
                TileResolutionActionKind.PropertyPlaceholder => ExecutePropertyTile(sessionId, session.GameState, resolution),
                TileResolutionActionKind.DeckPlaceholder => ExecuteCardTile(sessionId, session.GameState, resolution, player),
                _ => CreateError(
                    LobbyErrorCodes.UnsupportedTileEffect,
                    "This resolved tile effect is not supported yet."),
            };
        }
    }

    private LobbyServerEnvelope HandleEndTurn(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadLobbyPlayerPayload(root, out var sessionId, out var playerId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "end_turn requires payload.sessionId and payload.playerId.");
        }

        lock (sessionLock)
        {
            var session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSession,
                    "Session not found.");
            }

            if (!IsBoundToSessionPlayer(connectionContext, sessionId, playerId))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerSwitchRejected,
                    "This connection is not bound to that session and playerId.");
            }

            if (session.Status != GameSessionStatus.InGame)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Session is not in game.");
            }

            var player = session.GameState.Players.FirstOrDefault(
                gamePlayer => gamePlayer.PlayerId.Value == playerId);
            if (player is null)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotFound,
                    "Player is not in the game.");
            }

            if (session.GameState.CurrentTurnPlayerId?.Value != playerId)
            {
                return CreateError(
                    LobbyErrorCodes.NotYourTurn,
                    "It is not this player's turn.");
            }

            if (player.IsEliminated)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerEliminated,
                    "Eliminated players cannot end turns.");
            }

            if (!session.GameState.HasRolledThisTurn)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Player must roll before ending a turn.");
            }

            if (!session.GameState.HasResolvedTileThisTurn)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Player must resolve a tile before ending a turn.");
            }

            if (!session.GameState.HasExecutedTileThisTurn)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Player must execute a tile before ending a turn.");
            }

            if (session.GameState.ActiveAuctionState is not null)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Active auctions must be resolved before ending a turn.");
            }

            var previousPlayerId = player.PlayerId;
            var advancedGameState = TurnManager.AdvanceToNextTurn(session.GameState);

            _ = sessionManager.UpdateGameState(sessionId, advancedGameState);

            return CreateEndTurnResult(previousPlayerId, advancedGameState);
        }
    }

    private LobbyServerEnvelope HandlePlaceBid(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadPlaceBidPayload(root, out var sessionId, out var playerId, out var amount))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "place_bid requires payload.sessionId, payload.playerId, and positive integer payload.amount.");
        }

        lock (sessionLock)
        {
            var session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSession,
                    "Session not found.");
            }

            if (!IsBoundToSessionPlayer(connectionContext, sessionId, playerId))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerSwitchRejected,
                    "This connection is not bound to that session and playerId.");
            }

            if (session.Status != GameSessionStatus.InGame)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Session is not in game.");
            }

            var player = session.GameState.Players.FirstOrDefault(
                gamePlayer => gamePlayer.PlayerId.Value == playerId);
            if (player is null)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotFound,
                    "Player is not in the game.");
            }

            if (player.IsEliminated)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerEliminated,
                    "Eliminated players cannot bid in auctions.");
            }

            var activeAuctionState = session.GameState.ActiveAuctionState;
            if (activeAuctionState is null || !IsActiveAuctionStatus(activeAuctionState.Status))
            {
                return CreateError(
                    LobbyErrorCodes.AuctionNotActive,
                    "No active auction is available for bidding.");
            }

            var bidResult = AuctionManager.PlaceBid(
                session.GameState,
                activeAuctionState,
                player.PlayerId,
                new Money(amount),
                DateTimeOffset.UtcNow);

            if (!bidResult.BidAccepted)
            {
                return CreateBidRejectedError(bidResult);
            }

            var updatedGameState = session.GameState with
            {
                ActiveAuctionState = bidResult.AuctionState,
            };
            var updatedSession = sessionManager.UpdateGameState(sessionId, updatedGameState);
            var persistedAuctionState = updatedSession.GameState.ActiveAuctionState
                ?? throw new InvalidOperationException("Accepted auction bids must persist active auction state.");

            return CreateBidResult(player.PlayerId, amount, persistedAuctionState);
        }
    }

    private LobbyServerEnvelope HandleFinalizeAuction(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadLobbyPlayerPayload(root, out var sessionId, out var playerId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "finalize_auction requires payload.sessionId and payload.playerId.");
        }

        lock (sessionLock)
        {
            var session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSession,
                    "Session not found.");
            }

            if (!IsBoundToSessionPlayer(connectionContext, sessionId, playerId))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerSwitchRejected,
                    "This connection is not bound to that session and playerId.");
            }

            if (session.Status != GameSessionStatus.InGame)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Session is not in game.");
            }

            var player = session.GameState.Players.FirstOrDefault(
                gamePlayer => gamePlayer.PlayerId.Value == playerId);
            if (player is null)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotFound,
                    "Player is not in the game.");
            }

            if (session.GameState.CurrentTurnPlayerId?.Value != playerId)
            {
                return CreateError(
                    LobbyErrorCodes.NotYourTurn,
                    "It is not this player's turn.");
            }

            if (player.IsEliminated)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerEliminated,
                    "Eliminated players cannot finalize auctions.");
            }

            var activeAuctionState = session.GameState.ActiveAuctionState;
            if (activeAuctionState is null)
            {
                return CreateError(
                    LobbyErrorCodes.AuctionNotActive,
                    "No active auction is available for finalization.");
            }

            if (!IsActiveAuctionStatus(activeAuctionState.Status))
            {
                return CreateError(
                    LobbyErrorCodes.AuctionNotActive,
                    "No active auction is available for finalization.");
            }

            var finalizationResult = AuctionManager.FinalizeAuction(
                session.GameState,
                activeAuctionState);

            if (finalizationResult.ResultKind == AuctionFinalizationResultKind.InvalidAuctionState)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    finalizationResult.Message);
            }

            var updatedGameState = finalizationResult.GameState with
            {
                ActiveAuctionState = null,
            };
            var updatedSession = sessionManager.UpdateGameState(sessionId, updatedGameState);

            return CreateAuctionResult(finalizationResult, updatedSession.GameState);
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
            catch (InvalidOperationException exception)
                when (exception.Message == "Session is not in lobby status.")
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionStatus,
                    "Session is not in lobby status.");
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

            if (!IsBoundToSessionPlayer(connectionContext, sessionId, playerId))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerSwitchRejected,
                    "This connection is not bound to that session and playerId.");
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

    private LobbyServerEnvelope HandleSetReady(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadSetReadyPayload(root, out var sessionId, out var playerId, out var isReady))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "set_ready requires payload.sessionId, payload.playerId, and boolean payload.isReady.");
        }

        if (!IsBoundToSessionPlayer(connectionContext, sessionId, playerId))
        {
            return CreateError(
                LobbyErrorCodes.PlayerSwitchRejected,
                "This connection is not bound to that session and playerId.");
        }

        lock (sessionLock)
        {
            try
            {
                var updatedSession = sessionManager.SetReady(sessionId, new PlayerId(playerId), isReady);

                return CreateLobbyState(updatedSession);
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "Session not found.")
            {
                return CreateError(
                    LobbyErrorCodes.SessionNotFound,
                    "Session not found.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "Session is not in lobby status.")
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionStatus,
                    "Session is not in lobby status.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "Player is not in lobby.")
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotInLobby,
                    "Player is not in lobby.");
            }
        }
    }

    private LobbyServerEnvelope HandleStartGame(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadLobbyPlayerPayload(root, out var sessionId, out var playerId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "start_game requires payload.sessionId and payload.playerId.");
        }

        if (!IsBoundToSessionPlayer(connectionContext, sessionId, playerId))
        {
            return CreateError(
                LobbyErrorCodes.PlayerSwitchRejected,
                "This connection is not bound to that session and playerId.");
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

            if (session.Players.All(player => player.PlayerId.Value != playerId))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotInLobby,
                    "Player is not in lobby.");
            }

            try
            {
                var updatedSession = sessionManager.StartGame(sessionId);

                return CreateGameStarted(updatedSession);
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "Session not found.")
            {
                return CreateError(
                    LobbyErrorCodes.SessionNotFound,
                    "Session not found.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "Session is not in lobby status.")
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionStatus,
                    "Session is not in lobby status.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "Not enough players to start the game.")
            {
                return CreateError(
                    LobbyErrorCodes.NotEnoughPlayers,
                    "Not enough players to start the game.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "All players must be ready to start the game.")
            {
                return CreateError(
                    LobbyErrorCodes.PlayersNotReady,
                    "All players must be ready to start the game.");
            }
        }
    }

    private LobbyServerEnvelope ExecuteNoActionTile(
        string sessionId,
        GameState gameState,
        TileResolutionResult resolution)
    {
        var updatedGameState = gameState with
        {
            HasExecutedTileThisTurn = true,
        };

        _ = sessionManager.UpdateGameState(sessionId, updatedGameState);

        return CreateExecuteTileResult(
            resolution,
            "no_action",
            updatedGameState,
            auction: null,
            rent: null,
            card: null);
    }

    private LobbyServerEnvelope ExecutePropertyTile(
        string sessionId,
        GameState gameState,
        TileResolutionResult resolution)
    {
        var auctionStart = AuctionManager.StartMandatoryAuction(
            gameState,
            resolution.PlayerId,
            resolution.TileId);

        if (auctionStart.AuctionStarted)
        {
            var updatedGameState = gameState with
            {
                HasExecutedTileThisTurn = true,
                ActiveAuctionState = auctionStart.AuctionState,
            };

            _ = sessionManager.UpdateGameState(sessionId, updatedGameState);

            return CreateExecuteTileResult(
                resolution,
                "auction_started",
                updatedGameState,
                CreateAuctionPayload(auctionStart.AuctionState!),
                rent: null,
                card: null);
        }

        if (auctionStart.ResultKind != AuctionStartResultKind.PropertyAlreadyOwned)
        {
            return CreateError(
                LobbyErrorCodes.UnsupportedTileEffect,
                "This property tile effect is not supported yet.");
        }

        var rent = PropertyManager.PayRentForCurrentTile(gameState, resolution.PlayerId);
        var rentGameState = rent.GameState with
        {
            HasExecutedTileThisTurn = true,
        };

        _ = sessionManager.UpdateGameState(sessionId, rentGameState);

        return CreateExecuteTileResult(
            resolution,
            GetRentExecutionKind(rent),
            rentGameState,
            auction: null,
            rent: CreateRentPayload(rent, rentGameState),
            card: null);
    }

    private LobbyServerEnvelope ExecuteCardTile(
        string sessionId,
        GameState gameState,
        TileResolutionResult tileResolution,
        Player player)
    {
        if (!TryGetDeckIdForTileType(tileResolution.TileType, out var deckId))
        {
            return CreateError(
                LobbyErrorCodes.UnsupportedTileEffect,
                "This resolved tile effect is not supported yet.");
        }

        if (!gameState.CardDeckStates.TryGetValue(deckId, out var deckState))
        {
            return CreateError(
                LobbyErrorCodes.CardDeckNotFound,
                "Expected card deck state is missing from the game state.");
        }

        var drawResult = CardDeckManager.Draw(deckState);
        if (!drawResult.Succeeded || drawResult.DrawnCard is null)
        {
            return CreateError(
                LobbyErrorCodes.CardDeckEmpty,
                "Card draw pile is empty.");
        }

        var card = drawResult.DrawnCard;
        var cardResolution = CardResolver.ResolveCard(player, card);
        if (!cardResolution.IsValid)
        {
            return CreateError(
                LobbyErrorCodes.InvalidCard,
                "Drawn card cannot be resolved.");
        }

        if (!IsSupportedCardResolutionAction(cardResolution.ActionKind))
        {
            return CreateError(
                LobbyErrorCodes.UnsupportedCardAction,
                "Drawn card action is not supported yet.");
        }

        var executedGameState = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);
        var executedPlayer = executedGameState.Players.First(updatedPlayer => updatedPlayer.PlayerId == player.PlayerId);
        var finalDeckState = ShouldDiscardCard(cardResolution.ActionKind)
            ? CardDeckManager.Discard(drawResult.DeckState, card)
            : drawResult.DeckState;
        var cardDeckStates = new Dictionary<string, CardDeckState>(executedGameState.CardDeckStates)
        {
            [deckId] = finalDeckState,
        };
        var persistedGameState = executedGameState with
        {
            CardDeckStates = cardDeckStates,
            HasExecutedTileThisTurn = true,
        };

        _ = sessionManager.UpdateGameState(sessionId, persistedGameState);

        var executionKind = GetCardExecutionKind(cardResolution.ActionKind, executedPlayer);

        return CreateExecuteTileResult(
            tileResolution,
            executionKind,
            persistedGameState,
            auction: null,
            rent: null,
            card: CreateCardPayload(deckId, card, cardResolution, executionKind, executedPlayer));
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

        if (previousSession.Status == GameSessionStatus.Lobby)
        {
            _ = sessionManager.LeaveSession(
                connectionContext.SessionId,
                new PlayerConnection(
                    new PlayerId(playerId),
                    connectionContext.ConnectionId,
                    IsReady: false));
        }
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

    private static bool TryReadSetReadyPayload(
        JsonElement root,
        out string sessionId,
        out string playerId,
        out bool isReady)
    {
        sessionId = string.Empty;
        playerId = string.Empty;
        isReady = false;

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !TryReadString(payload, "sessionId", out sessionId) ||
            !TryReadString(payload, "playerId", out playerId) ||
            !payload.TryGetProperty("isReady", out var isReadyProperty) ||
            isReadyProperty.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        isReady = isReadyProperty.GetBoolean();
        return true;
    }

    private static bool TryReadPlaceBidPayload(
        JsonElement root,
        out string sessionId,
        out string playerId,
        out int amount)
    {
        sessionId = string.Empty;
        playerId = string.Empty;
        amount = 0;

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !TryReadString(payload, "sessionId", out sessionId) ||
            !TryReadString(payload, "playerId", out playerId) ||
            !payload.TryGetProperty("amount", out var amountProperty) ||
            amountProperty.ValueKind != JsonValueKind.Number ||
            !amountProperty.TryGetInt32(out amount) ||
            amount <= 0)
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
                FormatSessionStatus(session.Status),
                session.Players
                    .Select(player => new LobbyPlayerPayload(
                        player.PlayerId.Value,
                        player.ConnectionId,
                        player.IsReady))
                    .ToArray()));
    }

    private static LobbyServerEnvelope CreateGameStarted(GameSession session)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.GameStarted,
            new GameStartedPayload(
                session.SessionId,
                FormatSessionStatus(session.Status),
                FormatGamePhase(session.GameState.Phase),
                session.GameState.CurrentTurnPlayerId?.Value,
                session.GameState.Players
                    .Select(player => new GameStartedPlayerPayload(
                        player.PlayerId.Value,
                        player.Username,
                        player.TokenId,
                        player.ColorId,
                        player.CurrentTileId.Value,
                        player.Money.Amount))
                    .ToArray()));
    }

    private static LobbyServerEnvelope CreateRollResult(
        DiceRoll dice,
        MovementResult movementResult,
        bool hasRolledThisTurn)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.RollResult,
            new RollResultPayload(
                movementResult.PlayerId.Value,
                new[] { dice.FirstDie, dice.SecondDie },
                movementResult.LandingTileId.Value,
                movementResult.PassedStart,
                hasRolledThisTurn));
    }

    private static LobbyServerEnvelope CreateResolveTileResult(TileResolutionResult resolution)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.ResolveTileResult,
            new ResolveTileResultPayload(
                resolution.PlayerId.Value,
                resolution.TileId.Value,
                resolution.TileIndex,
                FormatTileType(resolution.TileType),
                RequiresTileAction(resolution.ActionKind),
                FormatTileResolutionActionKind(resolution.ActionKind)));
    }

    private static LobbyServerEnvelope CreateExecuteTileResult(
        TileResolutionResult resolution,
        string executionKind,
        GameState gameState,
        ExecuteTileAuctionPayload? auction,
        ExecuteTileRentPayload? rent,
        ExecuteTileCardPayload? card)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.ExecuteTileResult,
            new ExecuteTileResultPayload(
                resolution.PlayerId.Value,
                resolution.TileId.Value,
                resolution.TileIndex,
                FormatTileType(resolution.TileType),
                FormatTileResolutionActionKind(resolution.ActionKind),
                executionKind,
                FormatGamePhase(gameState.Phase),
                gameState.HasExecutedTileThisTurn,
                auction,
                rent,
                card));
    }

    private static LobbyServerEnvelope CreateEndTurnResult(
        PlayerId previousPlayerId,
        GameState gameState)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.EndTurnResult,
            new EndTurnResultPayload(
                previousPlayerId.Value,
                gameState.CurrentTurnPlayerId?.Value,
                gameState.TurnNumber));
    }

    private static LobbyServerEnvelope CreateBidResult(
        PlayerId bidderPlayerId,
        int amount,
        AuctionState auctionState)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.BidResult,
            new BidResultPayload(
                bidderPlayerId.Value,
                amount,
                auctionState.HighestBid?.Amount
                    ?? throw new InvalidOperationException("Accepted auction bids must have a highest bid."),
                auctionState.HighestBidderId?.Value
                    ?? throw new InvalidOperationException("Accepted auction bids must have a highest bidder.")));
    }

    private static LobbyServerEnvelope CreateAuctionResult(
        AuctionFinalizationResult finalizationResult,
        GameState persistedGameState)
    {
        var tileId = finalizationResult.PropertyTileId;
        var (resultType, winnerPlayerId, amount) = finalizationResult.ResultKind switch
        {
            AuctionFinalizationResultKind.FinalizedWithWinner => (
                "won",
                FindPropertyOwnerId(persistedGameState, tileId)
                    ?? throw new InvalidOperationException("Finalized auction winner must own the property in persisted state."),
                finalizationResult.WinningBid?.Amount
                    ?? throw new InvalidOperationException("Finalized auction winner must have a winning bid.")),
            AuctionFinalizationResultKind.FinalizedNoWinner => (
                "no_sale",
                null,
                0),
            AuctionFinalizationResultKind.WinnerFailedToPay => (
                "failed_payment",
                FindPersistedPlayerId(persistedGameState, finalizationResult.WinnerId)
                    ?? throw new InvalidOperationException("Failed auction payment must identify a persisted winner."),
                finalizationResult.WinningBid?.Amount
                    ?? throw new InvalidOperationException("Failed auction payment must have a winning bid.")),
            AuctionFinalizationResultKind.InvalidAuctionState => throw new InvalidOperationException(
                "Invalid auction finalization should be returned as an error."),
            _ => throw new InvalidOperationException("Unknown auction finalization result."),
        };

        return new LobbyServerEnvelope(
            LobbyMessageTypes.AuctionResult,
            new AuctionResultPayload(
                resultType,
                winnerPlayerId,
                amount,
                tileId.Value));
    }

    private static LobbyServerEnvelope CreateBidRejectedError(AuctionBidResult bidResult)
    {
        return bidResult.ResultKind switch
        {
            AuctionBidResultKind.BidderNotInGame => CreateError(
                LobbyErrorCodes.PlayerNotFound,
                bidResult.Message),
            AuctionBidResultKind.BidderEliminated => CreateError(
                LobbyErrorCodes.PlayerEliminated,
                bidResult.Message),
            AuctionBidResultKind.BidBelowStartingBid or
                AuctionBidResultKind.BidBelowMinimumIncrement => CreateError(
                LobbyErrorCodes.BidTooLow,
                bidResult.Message),
            _ => CreateError(
                LobbyErrorCodes.InvalidSessionState,
                bidResult.Message),
        };
    }

    private static ExecuteTileAuctionPayload CreateAuctionPayload(AuctionState auctionState)
    {
        return new ExecuteTileAuctionPayload(
            auctionState.PropertyTileId.Value,
            auctionState.TriggeringPlayerId.Value,
            FormatAuctionStatus(auctionState.Status),
            auctionState.StartingBid.Amount,
            auctionState.MinimumBidIncrement.Amount,
            auctionState.InitialPreBidSeconds,
            auctionState.BidResetSeconds,
            auctionState.HighestBid?.Amount,
            auctionState.HighestBidderId?.Value,
            auctionState.CountdownDurationSeconds);
    }

    private static ExecuteTileRentPayload CreateRentPayload(RentPaymentResult rent, GameState gameState)
    {
        var payer = gameState.Players.First(player => player.PlayerId == rent.LandingPlayerId);
        var owner = rent.OwnerId is null
            ? null
            : gameState.Players.First(player => player.PlayerId == rent.OwnerId.Value);

        return new ExecuteTileRentPayload(
            rent.LandingPlayerId.Value,
            rent.OwnerId?.Value,
            rent.RentDue.Amount,
            rent.RentPaid.Amount,
            payer.Money.Amount,
            owner?.Money.Amount,
            rent.PlayerEliminated,
            rent.EliminationResult is null ? null : FormatEliminationReason(rent.EliminationResult.Reason));
    }

    private static string GetRentExecutionKind(RentPaymentResult rent)
    {
        if (rent.PlayerEliminated)
        {
            return "rent_unpaid_player_eliminated";
        }

        return rent.RentCharged ? "rent_paid" : "rent_not_charged";
    }

    private static ExecuteTileCardPayload CreateCardPayload(
        string deckId,
        Card card,
        CardResolutionResult cardResolution,
        string executionKind,
        Player player)
    {
        return new ExecuteTileCardPayload(
            deckId,
            card.CardId.Value,
            card.DisplayName,
            FormatCardResolutionActionKind(cardResolution.ActionKind),
            executionKind,
            player.PlayerId.Value,
            player.CurrentTileId.Value,
            player.Money.Amount,
            player.IsEliminated,
            player.IsLockedUp,
            player.HeldCardIds.Select(cardId => cardId.Value).ToArray());
    }

    private static bool TryGetDeckIdForTileType(TileType tileType, out string deckId)
    {
        deckId = tileType switch
        {
            TileType.ChanceDeck => CardDeckIds.Chance,
            TileType.TableDeck => CardDeckIds.Table,
            _ => string.Empty,
        };

        return deckId.Length > 0;
    }

    private static bool IsSupportedCardResolutionAction(CardResolutionActionKind actionKind)
    {
        return actionKind is CardResolutionActionKind.MoveToStart or
            CardResolutionActionKind.MoveSteps or
            CardResolutionActionKind.ReceiveMoney or
            CardResolutionActionKind.PayMoney or
            CardResolutionActionKind.GoToLockup or
            CardResolutionActionKind.GetOutOfLockup;
    }

    private static bool ShouldDiscardCard(CardResolutionActionKind actionKind)
    {
        return actionKind != CardResolutionActionKind.GetOutOfLockup;
    }

    private static string GetCardExecutionKind(CardResolutionActionKind actionKind, Player player)
    {
        if (actionKind == CardResolutionActionKind.GetOutOfLockup)
        {
            return "card_held";
        }

        if (actionKind == CardResolutionActionKind.PayMoney && player.IsEliminated)
        {
            return "card_payment_eliminated_player";
        }

        return "card_executed";
    }

    private static LobbyServerEnvelope CreateError(string code, string message)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.Error,
            new LobbyErrorPayload(code, message));
    }

    private static bool IsBoundToSessionPlayer(
        LobbyConnectionContext connectionContext,
        string sessionId,
        string playerId)
    {
        return string.Equals(connectionContext.SessionId, sessionId, StringComparison.Ordinal) &&
            string.Equals(connectionContext.PlayerId, playerId, StringComparison.Ordinal);
    }

    private static string? FindPropertyOwnerId(GameState gameState, TileId propertyTileId)
    {
        foreach (var player in gameState.Players)
        {
            if (player.OwnedPropertyIds.Contains(propertyTileId))
            {
                return player.PlayerId.Value;
            }
        }

        return null;
    }

    private static string? FindPersistedPlayerId(GameState gameState, PlayerId? playerId)
    {
        if (playerId is null)
        {
            return null;
        }

        foreach (var player in gameState.Players)
        {
            if (player.PlayerId == playerId.Value)
            {
                return player.PlayerId.Value;
            }
        }

        return null;
    }

    private static string FormatSessionStatus(GameSessionStatus status)
    {
        return status switch
        {
            GameSessionStatus.Lobby => "lobby",
            GameSessionStatus.InGame => "in_game",
            GameSessionStatus.Finished => "finished",
            _ => status.ToString().ToLowerInvariant(),
        };
    }

    private static string FormatGamePhase(GamePhase phase)
    {
        return phase switch
        {
            GamePhase.Lobby => "lobby",
            GamePhase.AwaitingRoll => "awaiting_roll",
            GamePhase.ResolvingTurn => "resolving_turn",
            GamePhase.Auction => "auction",
            GamePhase.AwaitingEndTurn => "awaiting_end_turn",
            GamePhase.Completed => "completed",
            _ => phase.ToString().ToLowerInvariant(),
        };
    }

    private static string FormatTileType(TileType tileType)
    {
        return tileType switch
        {
            TileType.Start => "start",
            TileType.Property => "property",
            TileType.Transport => "transport",
            TileType.Utility => "utility",
            TileType.ChanceDeck => "chance_deck",
            TileType.TableDeck => "table_deck",
            TileType.Tax => "tax",
            TileType.Lockup => "lockup",
            TileType.GoToLockup => "go_to_lockup",
            TileType.FreeSpace => "free_space",
            _ => tileType.ToString().ToLowerInvariant(),
        };
    }

    private static string FormatTileResolutionActionKind(TileResolutionActionKind actionKind)
    {
        return actionKind switch
        {
            TileResolutionActionKind.NoAction => "no_action",
            TileResolutionActionKind.StartPlaceholder => "start_placeholder",
            TileResolutionActionKind.PropertyPlaceholder => "property_placeholder",
            TileResolutionActionKind.DeckPlaceholder => "deck_placeholder",
            TileResolutionActionKind.TaxPlaceholder => "tax_placeholder",
            TileResolutionActionKind.GoToLockupPlaceholder => "go_to_lockup_placeholder",
            _ => actionKind.ToString().ToLowerInvariant(),
        };
    }

    private static string FormatCardResolutionActionKind(CardResolutionActionKind actionKind)
    {
        return actionKind switch
        {
            CardResolutionActionKind.InvalidCard => "invalid_card",
            CardResolutionActionKind.MoveToStart => "move_to_start",
            CardResolutionActionKind.MoveToTile => "move_to_tile",
            CardResolutionActionKind.MoveSteps => "move_steps",
            CardResolutionActionKind.MoveToNearestTransport => "move_to_nearest_transport",
            CardResolutionActionKind.MoveToNearestUtility => "move_to_nearest_utility",
            CardResolutionActionKind.ReceiveMoney => "receive_money",
            CardResolutionActionKind.PayMoney => "pay_money",
            CardResolutionActionKind.ReceiveMoneyFromEveryPlayer => "receive_money_from_every_player",
            CardResolutionActionKind.PayMoneyToEveryPlayer => "pay_money_to_every_player",
            CardResolutionActionKind.RepairOwnedProperties => "repair_owned_properties",
            CardResolutionActionKind.GoToLockup => "go_to_lockup",
            CardResolutionActionKind.GetOutOfLockup => "get_out_of_lockup",
            _ => actionKind.ToString().ToLowerInvariant(),
        };
    }

    private static string FormatAuctionStatus(AuctionStatus status)
    {
        return status switch
        {
            AuctionStatus.AwaitingInitialBid => "awaiting_initial_bid",
            AuctionStatus.ActiveBidCountdown => "active_bid_countdown",
            _ => status.ToString().ToLowerInvariant(),
        };
    }

    private static bool IsActiveAuctionStatus(AuctionStatus status)
    {
        return status is AuctionStatus.AwaitingInitialBid or AuctionStatus.ActiveBidCountdown;
    }

    private static string FormatEliminationReason(EliminationReason reason)
    {
        return reason switch
        {
            EliminationReason.NegativeBalance => "negative_balance",
            EliminationReason.CannotFulfillPayment => "cannot_fulfill_payment",
            _ => reason.ToString().ToLowerInvariant(),
        };
    }

    private static bool RequiresTileAction(TileResolutionActionKind actionKind)
    {
        return actionKind switch
        {
            TileResolutionActionKind.PropertyPlaceholder or
                TileResolutionActionKind.DeckPlaceholder or
                TileResolutionActionKind.TaxPlaceholder or
                TileResolutionActionKind.GoToLockupPlaceholder => true,
            TileResolutionActionKind.NoAction or
                TileResolutionActionKind.StartPlaceholder => false,
            _ => false,
        };
    }
}
