namespace MonoJoey.Server.Realtime;

using System.Text.Json;
using MonoJoey.Server.GameEngine;
using MonoJoey.Server.Sessions;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public sealed class LobbyMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaximumSafeLoanPrincipal = int.MaxValue / 100;

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

        var result = HandleTextMessageResult(messageJson, connectionContext);

        return JsonSerializer.Serialize(result.DirectResponse, JsonOptions);
    }

    public LobbyMessageHandleResult HandleTextMessageResult(
        string messageJson,
        LobbyConnectionContext connectionContext)
    {
        ArgumentNullException.ThrowIfNull(messageJson);
        ArgumentNullException.ThrowIfNull(connectionContext);

        return HandleParsedMessage(messageJson, connectionContext);
    }

    public string CreateErrorMessage(string code, string message)
    {
        return JsonSerializer.Serialize(CreateError(code, message), JsonOptions);
    }

    public string SerializeBroadcastMessage(LobbyBroadcastEnvelope broadcast)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        return JsonSerializer.Serialize(broadcast, JsonOptions);
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
                else if (session.Status == GameSessionStatus.InGame)
                {
                    _ = sessionManager.ClearInGamePlayerConnection(
                        connectionContext.SessionId,
                        new PlayerId(connectionContext.PlayerId),
                        connectionContext.ConnectionId);
                }
            }

            connectionContext.ClearBinding();
        }
    }

    private LobbyMessageHandleResult HandleParsedMessage(
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
            LobbyMessageTypes.SetProfile => HandleSetProfile(root, connectionContext),
            LobbyMessageTypes.SetReady => HandleSetReady(root, connectionContext),
            LobbyMessageTypes.StartGame => HandleStartGame(root, connectionContext),
            LobbyMessageTypes.RollDice => HandleRollDice(root, connectionContext),
            LobbyMessageTypes.ResolveTile => HandleResolveTile(root, connectionContext),
            LobbyMessageTypes.ExecuteTile => HandleExecuteTile(root, connectionContext),
            LobbyMessageTypes.EndTurn => HandleEndTurn(root, connectionContext),
            LobbyMessageTypes.PlaceBid => HandlePlaceBid(root, connectionContext),
            LobbyMessageTypes.FinalizeAuction => HandleFinalizeAuction(root, connectionContext),
            LobbyMessageTypes.TakeLoan => HandleTakeLoan(root, connectionContext),
            LobbyMessageTypes.UseHeldCard => HandleUseHeldCard(root, connectionContext),
            LobbyMessageTypes.GetSnapshot => HandleGetSnapshot(root, connectionContext),
            LobbyMessageTypes.ReconnectSession => HandleReconnectSession(root, connectionContext),
            LobbyMessageTypes.LobbyState or
                LobbyMessageTypes.GameStarted or
                LobbyMessageTypes.RollResult or
                LobbyMessageTypes.ResolveTileResult or
                LobbyMessageTypes.ExecuteTileResult or
                LobbyMessageTypes.EndTurnResult or
                LobbyMessageTypes.BidResult or
                LobbyMessageTypes.AuctionResult or
                LobbyMessageTypes.LoanResult or
                LobbyMessageTypes.UseHeldCardResult or
                LobbyMessageTypes.SnapshotResult or
                LobbyMessageTypes.ReconnectResult or
                LobbyMessageTypes.DiceRolled or
                LobbyMessageTypes.TileResolved or
                LobbyMessageTypes.TileExecuted or
                LobbyMessageTypes.TurnEnded or
                LobbyMessageTypes.BidAccepted or
                LobbyMessageTypes.AuctionFinalized or
                LobbyMessageTypes.LoanTaken or
                LobbyMessageTypes.HeldCardUsed or
                LobbyMessageTypes.GameCompleted or
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

    private LobbyMessageHandleResult HandleRollDice(
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

            if (!IsCurrentInGamePlayerConnection(connectionContext, session, sessionId, playerId))
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

            if (session.GameState.Status == GameStatus.Completed)
            {
                return CreateGameAlreadyCompletedError();
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
            var rewardedGameState = movementResult.PassedStart
                ? ChangePlayerMoney(movementResult.GameState, player.PlayerId, DefaultTurnRules.PassStartReward)
                : movementResult.GameState;
            var updatedGameState = rewardedGameState with
            {
                HasRolledThisTurn = true,
                HasResolvedTileThisTurn = false,
                HasExecutedTileThisTurn = false,
            };

            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, updatedGameState);

            return CreateBroadcastResult(
                CreateRollResult(
                    dice,
                    movementResult.PlayerId,
                    movementResult.PassedStart,
                    persistence.Session.GameState),
                LobbyMessageTypes.DiceRolled,
                persistence.Session,
                persistence.Sequence);
        }
    }

    private LobbyMessageHandleResult HandleResolveTile(
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

            if (!IsCurrentInGamePlayerConnection(connectionContext, session, sessionId, playerId))
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

            if (session.GameState.Status == GameStatus.Completed)
            {
                return CreateGameAlreadyCompletedError();
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

            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, updatedGameState);

            return CreateBroadcastResult(
                CreateResolveTileResult(resolution),
                LobbyMessageTypes.TileResolved,
                persistence.Session,
                persistence.Sequence);
        }
    }

    private LobbyMessageHandleResult HandleExecuteTile(
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

            if (!IsCurrentInGamePlayerConnection(connectionContext, session, sessionId, playerId))
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

            if (session.GameState.Status == GameStatus.Completed)
            {
                return CreateGameAlreadyCompletedError();
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
                TileResolutionActionKind.TaxPlaceholder => ExecuteTaxTile(sessionId, session.GameState, resolution),
                TileResolutionActionKind.GoToLockupPlaceholder => ExecuteGoToLockupTile(sessionId, session.GameState, resolution),
                _ => CreateError(
                    LobbyErrorCodes.UnsupportedTileEffect,
                    "This resolved tile effect is not supported yet."),
            };
        }
    }

    private LobbyMessageHandleResult HandleEndTurn(
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

            if (!IsCurrentInGamePlayerConnection(connectionContext, session, sessionId, playerId))
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

            if (session.GameState.Status == GameStatus.Completed)
            {
                return CreateGameAlreadyCompletedError();
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

            var hasCompletedTurn =
                session.GameState.HasRolledThisTurn &&
                session.GameState.HasResolvedTileThisTurn &&
                session.GameState.HasExecutedTileThisTurn &&
                session.GameState.ActiveAuctionState is null;
            if (player.IsEliminated && !hasCompletedTurn)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerEliminated,
                    "Eliminated players cannot end incomplete turns.");
            }

            if (player.IsLockedUp && !hasCompletedTurn)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerLocked,
                    "Locked players cannot end incomplete turns.");
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

            var persistence = sessionManager.UpdateTerminalGameStateAndAllocateEventSequences(
                sessionId,
                advancedGameState,
                DateTimeOffset.UtcNow);

            return CreateTerminalBroadcastResult(
                CreateEndTurnResult(previousPlayerId, persistence.Session.GameState),
                LobbyMessageTypes.TurnEnded,
                persistence.Session,
                persistence);
        }
    }

    private LobbyMessageHandleResult HandlePlaceBid(
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

            if (!IsCurrentInGamePlayerConnection(connectionContext, session, sessionId, playerId))
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

            if (session.GameState.Status == GameStatus.Completed)
            {
                return CreateGameAlreadyCompletedError();
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
            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, updatedGameState);
            var persistedAuctionState = persistence.Session.GameState.ActiveAuctionState
                ?? throw new InvalidOperationException("Accepted auction bids must persist active auction state.");

            return CreateBroadcastResult(
                CreateBidResult(player.PlayerId, amount, persistedAuctionState),
                LobbyMessageTypes.BidAccepted,
                persistence.Session,
                persistence.Sequence);
        }
    }

    private LobbyMessageHandleResult HandleFinalizeAuction(
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

            if (!IsCurrentInGamePlayerConnection(connectionContext, session, sessionId, playerId))
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

            if (session.GameState.Status == GameStatus.Completed)
            {
                return CreateGameAlreadyCompletedError();
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
            var persistence = sessionManager.UpdateTerminalGameStateAndAllocateEventSequences(
                sessionId,
                updatedGameState,
                DateTimeOffset.UtcNow);

            return CreateTerminalBroadcastResult(
                CreateAuctionResult(finalizationResult, persistence.Session.GameState),
                LobbyMessageTypes.AuctionFinalized,
                persistence.Session,
                persistence);
        }
    }

    private LobbyMessageHandleResult HandleTakeLoan(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        var payloadResult = TryReadTakeLoanPayload(
            root,
            out var sessionId,
            out var playerId,
            out var amount,
            out var purpose);
        if (payloadResult == TakeLoanPayloadReadResult.InvalidPayload)
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "take_loan requires payload.sessionId, payload.playerId, integer payload.amount, and snake_case string payload.reason.");
        }

        if (payloadResult == TakeLoanPayloadReadResult.InvalidLoanAmount)
        {
            return CreateError(
                LobbyErrorCodes.InvalidLoanAmount,
                "Loan amount must be positive and within safe bounds.");
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

            if (!IsCurrentInGamePlayerConnection(connectionContext, session, sessionId, playerId))
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

            if (session.GameState.Status == GameStatus.Completed)
            {
                return CreateGameAlreadyCompletedError();
            }

            var gameState = session.GameState;
            var player = gameState.Players.FirstOrDefault(
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
                    "Eliminated players cannot take loans.");
            }

            if (!gameState.LoanSharkConfig.Enabled)
            {
                return CreateError(
                    LobbyErrorCodes.LoanModeDisabled,
                    "Loan Shark mode is disabled.");
            }

            if (!IsLoanAmountWithinSafeBounds(player, amount))
            {
                return CreateError(
                    LobbyErrorCodes.InvalidLoanAmount,
                    "Loan amount must be positive and within safe bounds.");
            }

            if (IsLoanPaymentBorrowPurpose(purpose))
            {
                var rejectedLoanResult = LoanManager.TakeLoan(
                    gameState,
                    player.PlayerId,
                    new Money(amount),
                    purpose);

                return CreateLoanRejectedError(rejectedLoanResult);
            }

            var activeAuctionState = gameState.ActiveAuctionState;
            var hasActiveAuction = activeAuctionState is not null && IsActiveAuctionStatus(activeAuctionState.Status);
            if (hasActiveAuction && purpose != BorrowPurpose.AuctionBid)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Only auction bid loans are allowed during an active auction.");
            }

            if (!hasActiveAuction && purpose == BorrowPurpose.AuctionBid)
            {
                return CreateError(
                    LobbyErrorCodes.AuctionNotActive,
                    "No active auction is available for auction bid loans.");
            }

            if (!hasActiveAuction &&
                purpose != BorrowPurpose.AuctionBid &&
                gameState.CurrentTurnPlayerId?.Value == playerId &&
                player.IsLockedUp)
            {
                return CreateError(
                    LobbyErrorCodes.PlayerLocked,
                    "Locked players cannot take loans outside auctions.");
            }

            if (purpose != BorrowPurpose.AuctionBid &&
                gameState.CurrentTurnPlayerId?.Value != playerId)
            {
                return CreateError(
                    LobbyErrorCodes.NotYourTurn,
                    "It is not this player's turn.");
            }

            var loanResult = LoanManager.TakeLoan(
                gameState,
                player.PlayerId,
                new Money(amount),
                purpose);

            if (!loanResult.LoanTaken)
            {
                return CreateLoanRejectedError(loanResult);
            }

            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, loanResult.GameState);
            var persistedPlayer = persistence.Session.GameState.Players.FirstOrDefault(
                gamePlayer => gamePlayer.PlayerId == player.PlayerId)
                ?? throw new InvalidOperationException("Accepted loans must persist the borrowing player.");

            return CreateBroadcastResult(
                CreateLoanResult(persistedPlayer, amount, purpose),
                LobbyMessageTypes.LoanTaken,
                persistence.Session,
                persistence.Sequence);
        }
    }

    private LobbyServerEnvelope HandleGetSnapshot(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadLobbyPlayerPayload(root, out var sessionId, out var playerId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "get_snapshot requires payload.sessionId and payload.playerId.");
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

            if (!IsCurrentInGamePlayerConnection(connectionContext, session, sessionId, playerId))
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

            var gameState = session.GameState;
            if (!gameState.Players.Any(gamePlayer => gamePlayer.PlayerId.Value == playerId))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotFound,
                    "Player is not in the game.");
            }

            return CreateSnapshotResult(gameState);
        }
    }

    private LobbyMessageHandleResult HandleUseHeldCard(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadUseHeldCardPayload(root, out var sessionId, out var playerId, out var cardId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "use_held_card requires payload.sessionId, payload.playerId, and payload.cardId.");
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

            if (!IsCurrentInGamePlayerConnection(connectionContext, session, sessionId, playerId))
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

            if (session.GameState.Status == GameStatus.Completed)
            {
                return CreateGameAlreadyCompletedError();
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
                    "Eliminated players cannot use held cards.");
            }

            var escapeId = new CardId(cardId);
            var escapeUse = LockupManager.UseGetOutOfLockupEscape(
                session.GameState,
                player.PlayerId,
                escapeId);
            if (escapeUse.Kind == LockupEscapeUseResultKind.PlayerNotLockedUp)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Player is not locked up.");
            }

            if (escapeUse.Kind == LockupEscapeUseResultKind.EscapeNotHeld)
            {
                return CreateError(
                    LobbyErrorCodes.HeldCardNotHeld,
                    "Player does not hold that lockup escape card.");
            }

            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, escapeUse.GameState);
            var persistedPlayer = persistence.Session.GameState.Players.First(
                gamePlayer => gamePlayer.PlayerId == player.PlayerId);

            return CreateBroadcastResult(
                CreateUseHeldCardResult(persistedPlayer, escapeId),
                LobbyMessageTypes.HeldCardUsed,
                persistence.Session,
                persistence.Sequence);
        }
    }

    private LobbyServerEnvelope HandleReconnectSession(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadLobbyPlayerPayload(root, out var sessionId, out var playerId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "reconnect_session requires payload.sessionId and payload.playerId.");
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

            if (session.Status != GameSessionStatus.InGame)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Session is not in game.");
            }

            if (!CanBindToRequestedSessionPlayer(connectionContext, sessionId, playerId))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerSwitchRejected,
                    "This connection is already bound to a different session or playerId.");
            }

            try
            {
                var updatedSession = sessionManager.RebindInGamePlayerConnection(
                    sessionId,
                    new PlayerId(playerId),
                    connectionContext.ConnectionId);

                connectionContext.Bind(sessionId, playerId);

                return CreateReconnectResult(updatedSession, playerId);
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "Player is not in the game." ||
                    exception.Message == "Player connection metadata not found.")
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotFound,
                    "Player is not in the game.");
            }
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

    private LobbyMessageHandleResult HandleSetProfile(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadSetProfilePayload(root, out var username, out var tokenId, out var colorId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "set_profile requires payload.username, payload.tokenId, and payload.colorId.");
        }

        if (connectionContext.SessionId is null || connectionContext.PlayerId is null)
        {
            return CreateError(
                LobbyErrorCodes.PlayerSwitchRejected,
                "This connection is not bound to a lobby session and playerId.");
        }

        lock (sessionLock)
        {
            try
            {
                var updatedSession = sessionManager.SetProfile(
                    connectionContext.SessionId,
                    new PlayerId(connectionContext.PlayerId),
                    username,
                    tokenId,
                    colorId);

                return CreateLobbyBroadcastResult(CreateLobbyState(updatedSession), updatedSession);
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
                when (exception.Message == "Profile fields must be non-empty.")
            {
                return CreateError(
                    LobbyErrorCodes.InvalidPayload,
                    "set_profile fields must be non-empty strings.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "Player is not in lobby.")
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotInLobby,
                    "Player is not in lobby.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "Username is already taken.")
            {
                return CreateError(
                    LobbyErrorCodes.UsernameTaken,
                    "Username is already taken.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "Token is already taken.")
            {
                return CreateError(
                    LobbyErrorCodes.TokenTaken,
                    "Token is already taken.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "Color is already taken.")
            {
                return CreateError(
                    LobbyErrorCodes.ColorTaken,
                    "Color is already taken.");
            }
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

    private LobbyMessageHandleResult ExecuteNoActionTile(
        string sessionId,
        GameState gameState,
        TileResolutionResult resolution)
    {
        var updatedGameState = gameState with
        {
            HasExecutedTileThisTurn = true,
        };

        var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, updatedGameState);

        return CreateBroadcastResult(
            CreateExecuteTileResult(
                resolution,
                "no_action",
                persistence.Session.GameState,
                auction: null,
                rent: null,
                card: null),
            LobbyMessageTypes.TileExecuted,
            persistence.Session,
            persistence.Sequence);
    }

    private LobbyMessageHandleResult ExecutePropertyTile(
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

            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, updatedGameState);
            var persistedAuctionState = persistence.Session.GameState.ActiveAuctionState
                ?? throw new InvalidOperationException("Started auctions must persist active auction state.");

            return CreateBroadcastResult(
                CreateExecuteTileResult(
                    resolution,
                    "auction_started",
                    persistence.Session.GameState,
                    CreateAuctionPayload(persistedAuctionState),
                    rent: null,
                    card: null),
                LobbyMessageTypes.TileExecuted,
                persistence.Session,
                persistence.Sequence);
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

        var rentPersistence = sessionManager.UpdateTerminalGameStateAndAllocateEventSequences(
            sessionId,
            rentGameState,
            DateTimeOffset.UtcNow);

        return CreateTerminalBroadcastResult(
            CreateExecuteTileResult(
                resolution,
                GetRentExecutionKind(rent),
                rentPersistence.Session.GameState,
                auction: null,
                rent: CreateRentPayload(rent, rentPersistence.Session.GameState),
                card: null),
            LobbyMessageTypes.TileExecuted,
            rentPersistence.Session,
            rentPersistence);
    }

    private LobbyMessageHandleResult ExecuteTaxTile(
        string sessionId,
        GameState gameState,
        TileResolutionResult resolution)
    {
        var taxedGameState = ChangePlayerMoney(gameState, resolution.PlayerId, new Money(-DefaultTurnRules.TaxAmount.Amount));
        var eliminatedGameState = BankruptcyManager.EliminateIfBankrupt(taxedGameState, resolution.PlayerId).GameState;
        var persistedGameState = eliminatedGameState with
        {
            HasExecutedTileThisTurn = true,
        };

        var persistence = sessionManager.UpdateTerminalGameStateAndAllocateEventSequences(
            sessionId,
            persistedGameState,
            DateTimeOffset.UtcNow);
        var persistedPlayer = persistence.Session.GameState.Players.First(player => player.PlayerId == resolution.PlayerId);
        var executionKind = persistedPlayer.IsEliminated ? "tax_eliminated_player" : "tax_paid";

        return CreateTerminalBroadcastResult(
            CreateExecuteTileResult(
                resolution,
                executionKind,
                persistence.Session.GameState,
                auction: null,
                rent: null,
                card: null),
            LobbyMessageTypes.TileExecuted,
            persistence.Session,
            persistence);
    }

    private LobbyMessageHandleResult ExecuteGoToLockupTile(
        string sessionId,
        GameState gameState,
        TileResolutionResult resolution)
    {
        var lockedGameState = LockupManager.SendToLockup(gameState, resolution.PlayerId) with
        {
            HasExecutedTileThisTurn = true,
        };

        var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, lockedGameState);

        return CreateBroadcastResult(
            CreateExecuteTileResult(
                resolution,
                "sent_to_lockup",
                persistence.Session.GameState,
                auction: null,
                rent: null,
                card: null),
            LobbyMessageTypes.TileExecuted,
            persistence.Session,
            persistence.Sequence);
    }

    private LobbyMessageHandleResult ExecuteCardTile(
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

        var persistence = sessionManager.UpdateTerminalGameStateAndAllocateEventSequences(
            sessionId,
            persistedGameState,
            DateTimeOffset.UtcNow);
        var persistedPlayer = persistence.Session.GameState.Players.First(
            updatedPlayer => updatedPlayer.PlayerId == player.PlayerId);
        var executionKind = GetCardExecutionKind(cardResolution.ActionKind, persistedPlayer);

        return CreateTerminalBroadcastResult(
            CreateExecuteTileResult(
                tileResolution,
                executionKind,
                persistence.Session.GameState,
                auction: null,
                rent: null,
                card: CreateCardPayload(deckId, card, cardResolution, executionKind, persistedPlayer)),
            LobbyMessageTypes.TileExecuted,
            persistence.Session,
            persistence);
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

    private static bool TryReadUseHeldCardPayload(
        JsonElement root,
        out string sessionId,
        out string playerId,
        out string cardId)
    {
        sessionId = string.Empty;
        playerId = string.Empty;
        cardId = string.Empty;

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !TryReadString(payload, "sessionId", out sessionId) ||
            !TryReadString(payload, "playerId", out playerId) ||
            !TryReadString(payload, "cardId", out cardId))
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

    private static bool TryReadSetProfilePayload(
        JsonElement root,
        out string username,
        out string tokenId,
        out string colorId)
    {
        username = string.Empty;
        tokenId = string.Empty;
        colorId = string.Empty;

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            payload.TryGetProperty("sessionId", out _) ||
            payload.TryGetProperty("playerId", out _) ||
            !TryReadString(payload, "username", out username) ||
            !TryReadString(payload, "tokenId", out tokenId) ||
            !TryReadString(payload, "colorId", out colorId))
        {
            return false;
        }

        username = username.Trim();
        tokenId = tokenId.Trim();
        colorId = colorId.Trim();

        return username.Length > 0 &&
            tokenId.Length > 0 &&
            colorId.Length > 0;
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

    private static TakeLoanPayloadReadResult TryReadTakeLoanPayload(
        JsonElement root,
        out string sessionId,
        out string playerId,
        out int amount,
        out BorrowPurpose purpose)
    {
        sessionId = string.Empty;
        playerId = string.Empty;
        amount = 0;
        purpose = default;

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !TryReadString(payload, "sessionId", out sessionId) ||
            !TryReadString(payload, "playerId", out playerId) ||
            !payload.TryGetProperty("amount", out var amountProperty) ||
            amountProperty.ValueKind != JsonValueKind.Number ||
            !amountProperty.TryGetInt32(out amount) ||
            !TryReadString(payload, "reason", out var reason) ||
            !TryParseBorrowPurpose(reason, out purpose))
        {
            return TakeLoanPayloadReadResult.InvalidPayload;
        }

        if (amount <= 0 || amount > MaximumSafeLoanPrincipal)
        {
            return TakeLoanPayloadReadResult.InvalidLoanAmount;
        }

        return TakeLoanPayloadReadResult.Success;
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
                        player.IsReady,
                        player.Username,
                        player.TokenId,
                        player.ColorId))
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

    private static LobbyServerEnvelope CreateSnapshotResult(GameState gameState)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.SnapshotResult,
            CreateSnapshotPayload(gameState));
    }

    private static LobbyServerEnvelope CreateReconnectResult(GameSession session, string playerId)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.ReconnectResult,
            new ReconnectResultPayload(
                session.SessionId,
                playerId,
                session.LastEventSequence,
                CreateSnapshotPayload(session.GameState)));
    }

    private static SnapshotPayload CreateSnapshotPayload(GameState gameState)
    {
        return new SnapshotPayload(
            SnapshotVersion: 1,
            SessionId: gameState.MatchId.Value,
            Status: "in_game",
            GameStatus: FormatGameStatus(gameState.Status),
            MatchId: gameState.MatchId.Value,
            Phase: FormatGamePhase(gameState.Phase),
            WinnerPlayerId: gameState.WinnerPlayerId?.Value,
            StartedAtUtc: gameState.StartedAtUtc,
            EndedAtUtc: gameState.EndedAtUtc,
            Turn: CreateSnapshotTurn(gameState),
            Players: gameState.Players
                .Select(CreateSnapshotPlayer)
                .ToArray(),
            Board: CreateSnapshotBoard(gameState),
            ActiveAuction: gameState.ActiveAuctionState is null
                ? null
                : CreateSnapshotAuction(gameState.ActiveAuctionState),
            CardDecks: gameState.CardDeckStates
                .OrderBy(deckState => deckState.Value.DeckId, StringComparer.Ordinal)
                .Select(deckState => CreateSnapshotCardDeck(deckState.Value))
                .ToArray(),
            LoanShark: new SnapshotLoanSharkPayload(gameState.LoanSharkConfig.Enabled));
    }

    private static GameCompletedPayload CreateGameCompletedPayload(GameState gameState)
    {
        if (gameState.WinnerPlayerId is null || gameState.EndedAtUtc is null)
        {
            throw new InvalidOperationException("Completed games must have a winner and end timestamp.");
        }

        var activePlayerCount = gameState.Players.Count(player => !player.IsBankrupt && !player.IsEliminated);
        var eliminatedPlayerIds = gameState.Players
            .Where(player => player.IsEliminated)
            .Select(player => player.PlayerId.Value)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();

        return new GameCompletedPayload(
            gameState.WinnerPlayerId.Value.Value,
            gameState.TurnNumber,
            gameState.EndedAtUtc.Value,
            activePlayerCount,
            eliminatedPlayerIds);
    }

    private static SnapshotTurnPayload CreateSnapshotTurn(GameState gameState)
    {
        return new SnapshotTurnPayload(
            gameState.CurrentTurnPlayerId?.Value,
            gameState.TurnNumber,
            gameState.HasRolledThisTurn,
            gameState.HasResolvedTileThisTurn,
            gameState.HasExecutedTileThisTurn);
    }

    private static SnapshotPlayerPayload CreateSnapshotPlayer(Player player)
    {
        return new SnapshotPlayerPayload(
            player.PlayerId.Value,
            player.Username,
            player.TokenId,
            player.ColorId,
            player.Money.Amount,
            player.CurrentTileId.Value,
            player.OwnedPropertyIds
                .Select(tileId => tileId.Value)
                .OrderBy(tileId => tileId, StringComparer.Ordinal)
                .ToArray(),
            player.HeldCardIds
                .Select(cardId => cardId.Value)
                .OrderBy(cardId => cardId, StringComparer.Ordinal)
                .ToArray(),
            CreateSnapshotLoan(player.LoanState),
            player.IsBankrupt,
            player.IsEliminated,
            player.IsLockedUp);
    }

    private static SnapshotPlayerLoanPayload CreateSnapshotLoan(PlayerLoanState? loanState)
    {
        return new SnapshotPlayerLoanPayload(
            loanState?.TotalBorrowed.Amount ?? 0,
            loanState?.CurrentInterestRatePercent ?? 0,
            loanState?.NextTurnInterestDue.Amount ?? 0,
            loanState?.LoanTier ?? 0);
    }

    private static SnapshotBoardPayload CreateSnapshotBoard(GameState gameState)
    {
        return new SnapshotBoardPayload(
            gameState.Board.BoardId.Value,
            gameState.Board.Version,
            gameState.Board.DisplayName,
            gameState.Board.Tiles
                .OrderBy(tile => tile.Index)
                .ThenBy(tile => tile.TileId.Value, StringComparer.Ordinal)
                .Select(tile => CreateSnapshotBoardTile(tile, gameState))
                .ToArray());
    }

    private static SnapshotBoardTilePayload CreateSnapshotBoardTile(Tile tile, GameState gameState)
    {
        return new SnapshotBoardTilePayload(
            tile.TileId.Value,
            tile.Index,
            tile.DisplayName,
            FormatTileType(tile.TileType),
            tile.GroupId,
            tile.Price?.Amount,
            tile.RentTable.Select(rent => rent.Amount).ToArray(),
            tile.UpgradeCost?.Amount,
            tile.IsPurchasable,
            tile.IsAuctionable,
            FindPropertyOwnerId(gameState, tile.TileId));
    }

    private static SnapshotAuctionPayload CreateSnapshotAuction(AuctionState auctionState)
    {
        return new SnapshotAuctionPayload(
            auctionState.PropertyTileId.Value,
            auctionState.TriggeringPlayerId.Value,
            FormatAuctionStatus(auctionState.Status),
            auctionState.StartingBid.Amount,
            auctionState.MinimumBidIncrement.Amount,
            auctionState.InitialPreBidSeconds,
            auctionState.BidResetSeconds,
            auctionState.HighestBid?.Amount,
            auctionState.HighestBidderId?.Value,
            auctionState.CountdownDurationSeconds,
            auctionState.Bids
                .Select(bid => new SnapshotAuctionBidPayload(
                    bid.BidderId.Value,
                    bid.Amount.Amount,
                    bid.PlacedAtUtc))
                .ToArray());
    }

    private static SnapshotCardDeckPayload CreateSnapshotCardDeck(CardDeckState deckState)
    {
        return new SnapshotCardDeckPayload(
            deckState.DeckId,
            deckState.DrawPile.Select(card => card.CardId.Value).ToArray(),
            deckState.DiscardPile.Select(card => card.CardId.Value).ToArray());
    }

    private static LobbyMessageHandleResult CreateBroadcastResult(
        LobbyServerEnvelope directResponse,
        string eventType,
        GameSession session,
        long sequence)
    {
        return new LobbyMessageHandleResult(
            directResponse,
            new LobbyBroadcastEnvelope(
                eventType,
                sequence,
                session.SessionId,
                session.GameState.MatchId.Value,
                DateTimeOffset.UtcNow,
                directResponse.Payload),
            CreateBroadcastTargetConnectionIds(session));
    }

    private static LobbyMessageHandleResult CreateLobbyBroadcastResult(
        LobbyServerEnvelope directResponse,
        GameSession session)
    {
        return new LobbyMessageHandleResult(
            directResponse,
            new LobbyBroadcastEnvelope(
                LobbyMessageTypes.LobbyState,
                session.LastEventSequence,
                session.SessionId,
                session.GameState.MatchId.Value,
                DateTimeOffset.UtcNow,
                directResponse.Payload),
            CreateLobbyBroadcastTargetConnectionIds(session));
    }

    private static LobbyMessageHandleResult CreateTerminalBroadcastResult(
        LobbyServerEnvelope directResponse,
        string eventType,
        GameSession session,
        GameStateEventPersistenceResult persistence)
    {
        if (persistence.CompletionSequence is null)
        {
            return CreateBroadcastResult(directResponse, eventType, session, persistence.Sequence);
        }

        var createdAtUtc = DateTimeOffset.UtcNow;
        var broadcasts = new[]
        {
            new LobbyBroadcastEnvelope(
                eventType,
                persistence.Sequence,
                session.SessionId,
                session.GameState.MatchId.Value,
                createdAtUtc,
                directResponse.Payload),
            new LobbyBroadcastEnvelope(
                LobbyMessageTypes.GameCompleted,
                persistence.CompletionSequence.Value,
                session.SessionId,
                session.GameState.MatchId.Value,
                createdAtUtc,
                CreateGameCompletedPayload(session.GameState)),
        };

        return new LobbyMessageHandleResult(
            directResponse,
            broadcasts,
            CreateBroadcastTargetConnectionIds(session));
    }

    private static IReadOnlyList<string> CreateBroadcastTargetConnectionIds(GameSession session)
    {
        if (session.Status != GameSessionStatus.InGame)
        {
            return Array.Empty<string>();
        }

        var gamePlayerIds = session.GameState.Players
            .Select(player => player.PlayerId.Value)
            .ToHashSet(StringComparer.Ordinal);

        return session.Players
            .Where(player => gamePlayerIds.Contains(player.PlayerId.Value))
            .Where(player => !string.IsNullOrWhiteSpace(player.ConnectionId))
            .Select(player => player.ConnectionId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> CreateLobbyBroadcastTargetConnectionIds(GameSession session)
    {
        if (session.Status != GameSessionStatus.Lobby)
        {
            return Array.Empty<string>();
        }

        return session.Players
            .Where(player => !string.IsNullOrWhiteSpace(player.ConnectionId))
            .Select(player => player.ConnectionId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static LobbyServerEnvelope CreateRollResult(
        DiceRoll dice,
        PlayerId playerId,
        bool passedStart,
        GameState gameState)
    {
        var persistedPlayer = gameState.Players.First(player => player.PlayerId == playerId);

        return new LobbyServerEnvelope(
            LobbyMessageTypes.RollResult,
            new RollResultPayload(
                persistedPlayer.PlayerId.Value,
                new[] { dice.FirstDie, dice.SecondDie },
                persistedPlayer.CurrentTileId.Value,
                passedStart,
                gameState.HasRolledThisTurn));
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

    private static LobbyServerEnvelope CreateLoanResult(
        Player persistedPlayer,
        int amount,
        BorrowPurpose purpose)
    {
        var loanState = persistedPlayer.LoanState
            ?? throw new InvalidOperationException("Accepted loans must persist loan state.");

        return new LobbyServerEnvelope(
            LobbyMessageTypes.LoanResult,
            new LoanResultPayload(
                persistedPlayer.PlayerId.Value,
                amount,
                FormatBorrowPurpose(purpose),
                persistedPlayer.Money.Amount,
                loanState.TotalBorrowed.Amount,
                loanState.CurrentInterestRatePercent,
                loanState.NextTurnInterestDue.Amount,
                loanState.LoanTier));
    }

    private static LobbyServerEnvelope CreateUseHeldCardResult(Player player, CardId cardId)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.UseHeldCardResult,
            new UseHeldCardResultPayload(
                player.PlayerId.Value,
                cardId.Value,
                player.IsLockedUp,
                player.HeldCardIds.Select(heldCardId => heldCardId.Value).OrderBy(heldCardId => heldCardId).ToArray()));
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

    private static LobbyServerEnvelope CreateLoanRejectedError(LoanTakeResult loanResult)
    {
        return loanResult.ResultKind switch
        {
            LoanTakeResultKind.InvalidAmount => CreateError(
                LobbyErrorCodes.InvalidLoanAmount,
                loanResult.Message),
            LoanTakeResultKind.DisallowedBorrowPurpose => CreateError(
                LobbyErrorCodes.LoanReasonBlocked,
                loanResult.Message),
            LoanTakeResultKind.PlayerNotInGame => CreateError(
                LobbyErrorCodes.PlayerNotFound,
                loanResult.Message),
            LoanTakeResultKind.PlayerEliminated => CreateError(
                LobbyErrorCodes.PlayerEliminated,
                loanResult.Message),
            _ => CreateError(
                LobbyErrorCodes.InvalidSessionState,
                loanResult.Message),
        };
    }

    private static LobbyServerEnvelope CreateGameAlreadyCompletedError()
    {
        return CreateError(
            LobbyErrorCodes.GameAlreadyCompleted,
            "Game is already completed.");
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
            CardResolutionActionKind.MoveToTile or
            CardResolutionActionKind.MoveSteps or
            CardResolutionActionKind.MoveToNearestTransport or
            CardResolutionActionKind.MoveToNearestUtility or
            CardResolutionActionKind.ReceiveMoney or
            CardResolutionActionKind.PayMoney or
            CardResolutionActionKind.ReceiveMoneyFromEveryPlayer or
            CardResolutionActionKind.PayMoneyToEveryPlayer or
            CardResolutionActionKind.RepairOwnedProperties or
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

    private static bool IsCurrentInGamePlayerConnection(
        LobbyConnectionContext connectionContext,
        GameSession session,
        string sessionId,
        string playerId)
    {
        if (!IsBoundToSessionPlayer(connectionContext, sessionId, playerId))
        {
            return false;
        }

        var playerConnection = session.Players.FirstOrDefault(
            player => player.PlayerId.Value == playerId);

        return playerConnection is not null &&
            string.Equals(playerConnection.ConnectionId, connectionContext.ConnectionId, StringComparison.Ordinal);
    }

    private static bool CanBindToRequestedSessionPlayer(
        LobbyConnectionContext connectionContext,
        string sessionId,
        string playerId)
    {
        if (!connectionContext.IsBound)
        {
            return true;
        }

        return IsBoundToSessionPlayer(connectionContext, sessionId, playerId);
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

    private static GameState ChangePlayerMoney(GameState gameState, PlayerId playerId, Money delta)
    {
        var players = gameState.Players.ToArray();
        for (var index = 0; index < players.Length; index++)
        {
            if (players[index].PlayerId != playerId)
            {
                continue;
            }

            players[index] = players[index] with
            {
                Money = new Money(players[index].Money.Amount + delta.Amount),
            };

            return gameState with { Players = players };
        }

        throw new InvalidOperationException("Player must exist before money can be changed.");
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

    private static string FormatGameStatus(GameStatus status)
    {
        return status switch
        {
            GameStatus.InProgress => "in_progress",
            GameStatus.Completed => "completed",
            _ => status.ToString().ToLowerInvariant(),
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

    private static bool TryParseBorrowPurpose(string reason, out BorrowPurpose purpose)
    {
        purpose = reason switch
        {
            "auction_bid" => BorrowPurpose.AuctionBid,
            "rent_payment" => BorrowPurpose.RentPayment,
            "tax_payment" => BorrowPurpose.TaxPayment,
            "card_penalty" => BorrowPurpose.CardPenalty,
            "fine" => BorrowPurpose.Fine,
            "loan_interest" => BorrowPurpose.LoanInterest,
            "loan_principal_repayment" => BorrowPurpose.LoanPrincipalRepayment,
            "existing_loan_debt" => BorrowPurpose.ExistingLoanDebt,
            _ => default,
        };

        return reason is "auction_bid" or
            "rent_payment" or
            "tax_payment" or
            "card_penalty" or
            "fine" or
            "loan_interest" or
            "loan_principal_repayment" or
            "existing_loan_debt";
    }

    private static string FormatBorrowPurpose(BorrowPurpose purpose)
    {
        return purpose switch
        {
            BorrowPurpose.AuctionBid => "auction_bid",
            BorrowPurpose.RentPayment => "rent_payment",
            BorrowPurpose.TaxPayment => "tax_payment",
            BorrowPurpose.CardPenalty => "card_penalty",
            BorrowPurpose.Fine => "fine",
            BorrowPurpose.LoanInterest => "loan_interest",
            BorrowPurpose.LoanPrincipalRepayment => "loan_principal_repayment",
            BorrowPurpose.ExistingLoanDebt => "existing_loan_debt",
            _ => throw new InvalidOperationException("Unknown borrow purpose."),
        };
    }

    private static bool IsLoanPaymentBorrowPurpose(BorrowPurpose purpose)
    {
        return purpose is BorrowPurpose.LoanInterest or
            BorrowPurpose.LoanPrincipalRepayment or
            BorrowPurpose.ExistingLoanDebt;
    }

    private static bool IsLoanAmountWithinSafeBounds(Player player, int amount)
    {
        var currentTotalBorrowed = player.LoanState?.TotalBorrowed.Amount ?? 0;
        return amount > 0 &&
            currentTotalBorrowed <= MaximumSafeLoanPrincipal - amount &&
            player.Money.Amount <= int.MaxValue - amount;
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

    private enum TakeLoanPayloadReadResult
    {
        Success,
        InvalidPayload,
        InvalidLoanAmount,
    }
}
