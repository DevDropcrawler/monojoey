namespace MonoJoey.Server.Realtime;

using System.Text.Json;
using MonoJoey.Server.GameEngine;
using MonoJoey.Server.GameEngine.Stats;
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
    private readonly AuctionTimerService auctionTimerService;
    private readonly IStatEventSink statEventSink;

    public LobbyMessageHandler(SessionManager sessionManager)
        : this(
            sessionManager,
            new DiceService(new RandomDiceRoller()),
            new AuctionTimerService(),
            NullStatEventSink.Instance)
    {
    }

    public LobbyMessageHandler(SessionManager sessionManager, DiceService diceService)
        : this(sessionManager, diceService, new AuctionTimerService(), NullStatEventSink.Instance)
    {
    }

    public LobbyMessageHandler(
        SessionManager sessionManager,
        DiceService diceService,
        AuctionTimerService auctionTimerService)
        : this(sessionManager, diceService, auctionTimerService, NullStatEventSink.Instance)
    {
    }

    internal LobbyMessageHandler(
        SessionManager sessionManager,
        DiceService diceService,
        AuctionTimerService auctionTimerService,
        IStatEventSink statEventSink)
    {
        this.sessionManager = sessionManager;
        this.diceService = diceService;
        this.auctionTimerService = auctionTimerService;
        this.statEventSink = statEventSink;
    }

    internal AuctionTimerService AuctionTimerService => auctionTimerService;

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
            LobbyMessageTypes.SetRules => HandleSetRules(root, connectionContext),
            LobbyMessageTypes.StartGame => HandleStartGame(root, connectionContext),
            LobbyMessageTypes.RollDice => HandleRollDice(root, connectionContext),
            LobbyMessageTypes.ResolveTile => HandleResolveTile(root, connectionContext),
            LobbyMessageTypes.ExecuteTile => HandleExecuteTile(root, connectionContext),
            LobbyMessageTypes.EndTurn => HandleEndTurn(root, connectionContext),
            LobbyMessageTypes.PlaceBid => HandlePlaceBid(root, connectionContext),
            LobbyMessageTypes.FinalizeAuction => HandleFinalizeAuction(root, connectionContext),
            LobbyMessageTypes.TakeLoan => HandleTakeLoan(root, connectionContext),
            LobbyMessageTypes.MortgageProperty => HandleMortgageProperty(root, connectionContext),
            LobbyMessageTypes.UnmortgageProperty => HandleUnmortgageProperty(root, connectionContext),
            LobbyMessageTypes.UpgradeProperty => HandleUpgradeProperty(root, connectionContext),
            LobbyMessageTypes.UseHeldCard => HandleUseHeldCard(root, connectionContext),
            LobbyMessageTypes.CreateTradeOffer => HandleCreateTradeOffer(root, connectionContext),
            LobbyMessageTypes.AcceptTradeOffer => HandleAcceptTradeOffer(root, connectionContext),
            LobbyMessageTypes.DeclineTradeOffer => HandleDeclineTradeOffer(root, connectionContext),
            LobbyMessageTypes.CancelTradeOffer => HandleCancelTradeOffer(root, connectionContext),
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
                LobbyMessageTypes.MortgageResult or
                LobbyMessageTypes.UnmortgageResult or
                LobbyMessageTypes.UpgradeResult or
                LobbyMessageTypes.UseHeldCardResult or
                LobbyMessageTypes.TradeOfferResult or
                LobbyMessageTypes.TradeAcceptResult or
                LobbyMessageTypes.TradeDeclineResult or
                LobbyMessageTypes.TradeCancelResult or
                LobbyMessageTypes.SnapshotResult or
                LobbyMessageTypes.ReconnectResult or
                LobbyMessageTypes.RulesUpdated or
                LobbyMessageTypes.DiceRolled or
                LobbyMessageTypes.TileResolved or
                LobbyMessageTypes.TileExecuted or
                LobbyMessageTypes.TurnEnded or
                LobbyMessageTypes.BidAccepted or
                LobbyMessageTypes.AuctionFinalized or
                LobbyMessageTypes.LoanTaken or
                LobbyMessageTypes.PropertyMortgaged or
                LobbyMessageTypes.PropertyUnmortgaged or
                LobbyMessageTypes.PropertyUpgraded or
                LobbyMessageTypes.HeldCardUsed or
                LobbyMessageTypes.TradeOfferCreated or
                LobbyMessageTypes.TradeOfferAccepted or
                LobbyMessageTypes.TradeOfferDeclined or
                LobbyMessageTypes.TradeOfferCancelled or
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

            if (session.GameState.HasRolledThisTurn)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    "Player has already rolled this turn.");
            }

            var dice = diceService.RollDice(session.GameState.Rules.Dice.SidesPerDie);
            if (player.IsLockedUp && session.GameState.Rules.Jail.Enabled)
            {
                return ResolveLockedPlayerRoll(sessionId, session.GameState, player, dice);
            }

            var rollStartGameState = player.IsLockedUp
                ? LockupManager.ReleaseFromLockup(session.GameState, player.PlayerId, "jail_disabled") with
                {
                    SuppressDoublesExtraTurnThisTurn = true,
                }
                : session.GameState;
            var rollStartPlayer = rollStartGameState.Players.First(gamePlayer => gamePlayer.PlayerId == player.PlayerId);
            var isSlimed = PlayerStatusEffectManager.HasSlimer(rollStartPlayer);
            var movementSteps = isSlimed ? dice.FirstDie : dice.Total;

            var turnStateGameState = PlayerTurnStateManager.ApplyDiceRoll(
                rollStartGameState,
                player.PlayerId,
                dice.IsDouble);
            var rolledPlayer = turnStateGameState.Players.First(gamePlayer => gamePlayer.PlayerId == player.PlayerId);
            if (ShouldSendToLockupForConsecutiveDoubles(turnStateGameState, rolledPlayer, dice))
            {
                var lockedGameState = LockupManager.SendToLockup(turnStateGameState, player.PlayerId) with
                {
                    HasRolledThisTurn = true,
                    HasResolvedTileThisTurn = true,
                    HasExecutedTileThisTurn = true,
                    SuppressDoublesExtraTurnThisTurn = true,
                };
                var lockupPersistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, lockedGameState);

                return CreateBroadcastResult(
                    CreateRollResult(
                        dice,
                        player.PlayerId,
                        rollStartGameState,
                        lockupPersistence.Session.GameState,
                        movement: CreateDirectMovementPayload(
                            rollStartGameState,
                            lockupPersistence.Session.GameState,
                            player.PlayerId,
                            "direct"),
                        moneyDeltas: null,
                        rollKind: "triple_doubles_lockup"),
                    LobbyMessageTypes.DiceRolled,
                    lockupPersistence.Session,
                    lockupPersistence.Sequence);
            }

            var movementResult = MovementManager.MovePlayer(
                turnStateGameState,
                player.PlayerId,
                movementSteps);
            var passStartReward = new Money(rollStartGameState.Rules.Economy.PassStartReward);
            var rewardedGameState = movementResult.PassedStart
                ? ChangePlayerMoney(movementResult.GameState, player.PlayerId, passStartReward)
                : movementResult.GameState;
            var statusGameState = isSlimed && dice.FirstDie == 6
                ? PlayerStatusEffectManager.RemoveSlimer(rewardedGameState, player.PlayerId)
                : rewardedGameState;
            var updatedGameState = statusGameState with
            {
                HasRolledThisTurn = true,
                HasResolvedTileThisTurn = false,
                HasExecutedTileThisTurn = false,
            };

            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, updatedGameState);

            return CreateBroadcastResult(
                CreateRollResult(
                    dice,
                    player.PlayerId,
                    rollStartGameState,
                    persistence.Session.GameState,
                    CreateMovementPayload(movementResult),
                    CreateRollMoneyDeltas(
                        rollStartGameState,
                        persistence.Session.GameState,
                        movementResult,
                        fineAmount: null),
                    rollKind: "normal"),
                LobbyMessageTypes.DiceRolled,
                persistence.Session,
                persistence.Sequence);
        }
    }

    private LobbyMessageHandleResult ResolveLockedPlayerRoll(
        string sessionId,
        GameState gameState,
        Player player,
        DiceRoll dice)
    {
        if (dice.IsDouble)
        {
            var releasedGameState = LockupManager.ReleaseFromLockup(gameState, player.PlayerId, "jail_doubles") with
            {
                SuppressDoublesExtraTurnThisTurn = true,
            };
            var movementResult = MovementManager.MovePlayer(releasedGameState, player.PlayerId, dice.Total);
            var passStartReward = new Money(gameState.Rules.Economy.PassStartReward);
            var rewardedGameState = movementResult.PassedStart
                ? ChangePlayerMoney(movementResult.GameState, player.PlayerId, passStartReward)
                : movementResult.GameState;
            var updatedGameState = rewardedGameState with
            {
                HasRolledThisTurn = true,
                HasResolvedTileThisTurn = false,
                HasExecutedTileThisTurn = false,
                SuppressDoublesExtraTurnThisTurn = true,
            };
            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, updatedGameState);

            return CreateBroadcastResult(
                CreateRollResult(
                    dice,
                    player.PlayerId,
                    gameState,
                    persistence.Session.GameState,
                    CreateMovementPayload(movementResult),
                    CreateRollMoneyDeltas(
                        gameState,
                        persistence.Session.GameState,
                        movementResult,
                        fineAmount: null),
                    rollKind: "jail_doubles_release"),
                LobbyMessageTypes.DiceRolled,
                persistence.Session,
                persistence.Sequence);
        }

        var failedAttemptGameState = PlayerTurnStateManager.ApplyFailedJailRoll(gameState, player.PlayerId);
        var failedAttemptPlayer = failedAttemptGameState.Players.First(
            gamePlayer => gamePlayer.PlayerId == player.PlayerId);
        if (failedAttemptPlayer.TurnState.JailRollAttemptCount >= gameState.Rules.Jail.MaxTurns &&
            gameState.Rules.Jail.MaxTurnFailureAction == JailRules.PayFineAndReleaseMaxTurnFailureAction)
        {
            var finePayment = LockupManager.PayFineAndRelease(
                failedAttemptGameState,
                player.PlayerId,
                forcePayment: true);
            if (finePayment.Kind == LockupFinePaymentResultKind.PaidAndReleased)
            {
                var movementResult = MovementManager.MovePlayer(finePayment.GameState, player.PlayerId, dice.Total);
                var passStartReward = new Money(gameState.Rules.Economy.PassStartReward);
                var rewardedGameState = movementResult.PassedStart
                    ? ChangePlayerMoney(movementResult.GameState, player.PlayerId, passStartReward)
                    : movementResult.GameState;
                var updatedGameState = rewardedGameState with
                {
                    HasRolledThisTurn = true,
                    HasResolvedTileThisTurn = false,
                    HasExecutedTileThisTurn = false,
                    SuppressDoublesExtraTurnThisTurn = true,
                };
                var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, updatedGameState);

                return CreateBroadcastResult(
                    CreateRollResult(
                        dice,
                        player.PlayerId,
                        gameState,
                        persistence.Session.GameState,
                        CreateMovementPayload(movementResult),
                        CreateRollMoneyDeltas(
                            gameState,
                            persistence.Session.GameState,
                            movementResult,
                            finePayment.FineAmount),
                        rollKind: "jail_max_attempts_paid_release"),
                    LobbyMessageTypes.DiceRolled,
                    persistence.Session,
                    persistence.Sequence);
            }
        }

        var completedFailedAttemptGameState = failedAttemptGameState with
        {
            HasRolledThisTurn = true,
            HasResolvedTileThisTurn = true,
            HasExecutedTileThisTurn = true,
            SuppressDoublesExtraTurnThisTurn = true,
        };
        var failedPersistence = sessionManager.UpdateGameStateAndAllocateEventSequence(
            sessionId,
            completedFailedAttemptGameState);
        var persistedPlayer = failedPersistence.Session.GameState.Players.First(
            gamePlayer => gamePlayer.PlayerId == player.PlayerId);

        return CreateBroadcastResult(
            CreateRollResult(
                dice,
                player.PlayerId,
                gameState,
                failedPersistence.Session.GameState,
                movement: null,
                moneyDeltas: null,
                rollKind: persistedPlayer.TurnState.JailRollAttemptCount >= gameState.Rules.Jail.MaxTurns
                    ? "jail_max_attempts_fine_unpaid"
                    : "jail_failed_attempt"),
            LobbyMessageTypes.DiceRolled,
            failedPersistence.Session,
            failedPersistence.Sequence);
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
            var beforeAdvanceGameState = session.GameState;
            var advancedGameState = ShouldGrantDoublesExtraTurn(beforeAdvanceGameState, player)
                ? TurnManager.AdvanceToExtraTurn(beforeAdvanceGameState)
                : TurnManager.AdvanceToNextTurn(beforeAdvanceGameState);

            var persistence = sessionManager.UpdateTerminalGameStateAndAllocateEventSequences(
                sessionId,
                advancedGameState,
                DateTimeOffset.UtcNow);

            var directResponse = CreateEndTurnResult(
                previousPlayerId,
                beforeAdvanceGameState,
                persistence.Session.GameState);
            var result = CreateTerminalBroadcastResult(
                directResponse,
                LobbyMessageTypes.TurnEnded,
                persistence.Session,
                persistence);

            EmitPropertyRepairStats(beforeAdvanceGameState, persistence.Session.GameState);
            EmitGameWonStat(persistence, persistence.Session.GameState);

            return result;
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

            var bidAcceptedAtUtc = DateTimeOffset.UtcNow;
            var bidResult = AuctionManager.PlaceBid(
                session.GameState,
                activeAuctionState,
                player.PlayerId,
                new Money(amount),
                bidAcceptedAtUtc);

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
            ScheduleAuctionTimer(sessionId, persistedAuctionState);

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
            auctionTimerService.Cancel(sessionId);

            var directResponse = CreateAuctionResult(finalizationResult, persistence.Session.GameState);
            var result = CreateTerminalBroadcastResult(
                directResponse,
                LobbyMessageTypes.AuctionFinalized,
                persistence.Session,
                persistence);

            EmitAuctionStats(finalizationResult);
            EmitGameWonStat(persistence, persistence.Session.GameState);

            return result;
        }
    }

    internal LobbyMessageHandleResult? HandleAuctionTimerExpired(
        string sessionId,
        DateTimeOffset expiredTimerEndsAtUtc,
        DateTimeOffset? serverNowUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        lock (sessionLock)
        {
            var session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                auctionTimerService.Cancel(sessionId);
                return null;
            }

            if (session.Status != GameSessionStatus.InGame ||
                session.GameState.Status == GameStatus.Completed)
            {
                auctionTimerService.Cancel(sessionId);
                return null;
            }

            var activeAuctionState = session.GameState.ActiveAuctionState;
            if (activeAuctionState is null || !IsActiveAuctionStatus(activeAuctionState.Status))
            {
                auctionTimerService.Cancel(sessionId);
                return null;
            }

            if (activeAuctionState.TimerEndsAtUtc != expiredTimerEndsAtUtc)
            {
                return null;
            }

            var nowUtc = serverNowUtc ?? DateTimeOffset.UtcNow;
            if (nowUtc < expiredTimerEndsAtUtc)
            {
                return null;
            }

            var finalizationResult = AuctionManager.FinalizeAuction(
                session.GameState,
                activeAuctionState);

            if (finalizationResult.ResultKind == AuctionFinalizationResultKind.InvalidAuctionState)
            {
                return null;
            }

            var updatedGameState = finalizationResult.GameState with
            {
                ActiveAuctionState = null,
            };
            var persistence = sessionManager.UpdateTerminalGameStateAndAllocateEventSequences(
                sessionId,
                updatedGameState,
                nowUtc);
            auctionTimerService.Cancel(sessionId);

            var directResponse = CreateAuctionResult(finalizationResult, persistence.Session.GameState);
            var result = CreateTerminalBroadcastResult(
                directResponse,
                LobbyMessageTypes.AuctionFinalized,
                persistence.Session,
                persistence);

            EmitAuctionStats(finalizationResult);
            EmitGameWonStat(persistence, persistence.Session.GameState);

            return result;
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

            var loanConfig = LoanSharkConfig.FromRules(gameState.Rules.Loans);
            if (!loanConfig.Enabled)
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

            if (IsLoanPaymentBorrowPurpose(purpose) && !loanConfig.CanBorrowForLoanPayments)
            {
                var rejectedLoanResult = LoanManager.TakeLoan(
                    gameState,
                    player.PlayerId,
                    new Money(amount),
                    purpose,
                    loanConfig);

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
                purpose,
                loanConfig);

            if (!loanResult.LoanTaken)
            {
                return CreateLoanRejectedError(loanResult);
            }

            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, loanResult.GameState);
            var persistedPlayer = persistence.Session.GameState.Players.FirstOrDefault(
                gamePlayer => gamePlayer.PlayerId == player.PlayerId)
                ?? throw new InvalidOperationException("Accepted loans must persist the borrowing player.");

            var directResponse = CreateLoanResult(persistedPlayer, amount, purpose);
            var result = CreateBroadcastResult(
                directResponse,
                LobbyMessageTypes.LoanTaken,
                persistence.Session,
                persistence.Sequence);

            EmitStatEvent(new StatEvent(
                player.PlayerId,
                StatEventKind.LoanTaken,
                new Money(amount),
                Source: "loan"));

            return result;
        }
    }

    private LobbyMessageHandleResult HandleMortgageProperty(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadPropertyMortgagePayload(root, out var sessionId, out var playerId, out var propertyTileId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "mortgage_property requires payload.sessionId, payload.playerId, and payload.propertyTileId.");
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

            var mortgageResult = MortgageManager.MortgageProperty(
                session.GameState,
                new PlayerId(playerId),
                new TileId(propertyTileId));
            if (!mortgageResult.MortgageAccepted)
            {
                return CreateMortgageRejectedError(mortgageResult);
            }

            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, mortgageResult.GameState);
            var persistedPlayer = persistence.Session.GameState.Players.First(player =>
                player.PlayerId == mortgageResult.PlayerId);
            var persistedResult = mortgageResult with
            {
                GameState = persistence.Session.GameState,
                Money = persistedPlayer.Money,
            };

            return CreateBroadcastResult(
                CreateMortgageResult(persistedResult),
                LobbyMessageTypes.PropertyMortgaged,
                persistence.Session,
                persistence.Sequence);
        }
    }

    private LobbyMessageHandleResult HandleUnmortgageProperty(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadPropertyMortgagePayload(root, out var sessionId, out var playerId, out var propertyTileId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "unmortgage_property requires payload.sessionId, payload.playerId, and payload.propertyTileId.");
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

            var unmortgageResult = MortgageManager.UnmortgageProperty(
                session.GameState,
                new PlayerId(playerId),
                new TileId(propertyTileId));
            if (!unmortgageResult.UnmortgageAccepted)
            {
                return CreateUnmortgageRejectedError(unmortgageResult);
            }

            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, unmortgageResult.GameState);
            var persistedPlayer = persistence.Session.GameState.Players.First(player =>
                player.PlayerId == unmortgageResult.PlayerId);
            var persistedResult = unmortgageResult with
            {
                GameState = persistence.Session.GameState,
                Money = persistedPlayer.Money,
            };

            return CreateBroadcastResult(
                CreateUnmortgageResult(persistedResult),
                LobbyMessageTypes.PropertyUnmortgaged,
                persistence.Session,
                persistence.Sequence);
        }
    }

    private LobbyMessageHandleResult HandleUpgradeProperty(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadPropertyMortgagePayload(root, out var sessionId, out var playerId, out var propertyTileId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "upgrade_property requires payload.sessionId, payload.playerId, and payload.propertyTileId.");
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

            var upgradeResult = PropertyUpgradeManager.BuyUpgrade(
                session.GameState,
                new PlayerId(playerId),
                new TileId(propertyTileId));
            if (!upgradeResult.UpgradeBought)
            {
                return CreateUpgradeRejectedError(upgradeResult, session.GameState.Status);
            }

            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, upgradeResult.GameState);
            var persistedPlayer = persistence.Session.GameState.Players.First(player =>
                player.PlayerId == upgradeResult.PlayerId);
            var persistedResult = upgradeResult with
            {
                GameState = persistence.Session.GameState,
                Money = persistedPlayer.Money,
            };

            return CreateBroadcastResult(
                CreateUpgradeResult(persistedResult),
                LobbyMessageTypes.PropertyUpgraded,
                persistence.Session,
                persistence.Sequence);
        }
    }

    private LobbyMessageHandleResult HandleCreateTradeOffer(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadCreateTradeOfferPayload(
            root,
            out var sessionId,
            out var playerId,
            out var recipientPlayerId,
            out var offered,
            out var requested))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "create_trade_offer requires payload.sessionId, payload.playerId, payload.recipientPlayerId, and valid offered/requested assets.");
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

            var proposerId = new PlayerId(playerId);
            var recipientId = new PlayerId(recipientPlayerId);
            if (session.PendingTradeOffers.Any(offer => offer.ProposerPlayerId == proposerId))
            {
                return CreateError(
                    LobbyErrorCodes.TradeOfferActive,
                    "Player already has an active outgoing trade offer.");
            }

            var validation = TradeManager.ValidateTrade(
                session.GameState,
                proposerId,
                offered,
                recipientId,
                requested);
            if (!validation.TradeValid)
            {
                return CreateTradeValidationRejectedError(validation);
            }

            var persistence = sessionManager.AddPendingTradeOfferAndAllocateEventSequence(
                sessionId,
                proposerId,
                recipientId,
                validation.FirstPlayerAssets,
                validation.SecondPlayerAssets,
                DateTimeOffset.UtcNow);
            var persistedOffer = persistence.Session.PendingTradeOffers.Single(
                offer => offer.CreatedSequence == persistence.Sequence);

            return CreateBroadcastResult(
                CreateTradeOfferResult(persistedOffer),
                LobbyMessageTypes.TradeOfferCreated,
                persistence.Session,
                persistence.Sequence);
        }
    }

    private LobbyMessageHandleResult HandleAcceptTradeOffer(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadTradeOfferLifecyclePayload(root, out var sessionId, out var playerId, out var tradeOfferId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "accept_trade_offer requires payload.sessionId, payload.playerId, and payload.tradeOfferId.");
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

            var offer = FindPendingTradeOffer(session, tradeOfferId);
            if (offer is null)
            {
                return CreateError(
                    LobbyErrorCodes.TradeOfferNotFound,
                    "Trade offer was not found.");
            }

            var requesterId = new PlayerId(playerId);
            if (offer.RecipientPlayerId != requesterId)
            {
                return CreateError(
                    LobbyErrorCodes.TradeOfferNotForPlayer,
                    "Only the trade offer recipient can accept it.");
            }

            var validation = TradeManager.ValidateTrade(
                session.GameState,
                offer.ProposerPlayerId,
                offer.Offered,
                offer.RecipientPlayerId,
                offer.Requested);
            if (!validation.TradeValid)
            {
                return CreateTradeValidationRejectedError(validation);
            }

            var settlement = TradeManager.SettleTrade(
                session.GameState,
                offer.ProposerPlayerId,
                validation.FirstPlayerAssets,
                offer.RecipientPlayerId,
                validation.SecondPlayerAssets);
            if (!settlement.TradeSettled)
            {
                return CreateTradeSettlementRejectedError(settlement);
            }

            var persistence = sessionManager.UpdateGameStateAndRemovePendingTradeOfferAndAllocateEventSequence(
                sessionId,
                settlement.GameState,
                offer.TradeOfferId);

            return CreateBroadcastResult(
                CreateTradeAcceptResult(offer, session.GameState, persistence.Session.GameState, settlement),
                LobbyMessageTypes.TradeOfferAccepted,
                persistence.Session,
                persistence.Sequence);
        }
    }

    private LobbyMessageHandleResult HandleDeclineTradeOffer(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadTradeOfferLifecyclePayload(root, out var sessionId, out var playerId, out var tradeOfferId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "decline_trade_offer requires payload.sessionId, payload.playerId, and payload.tradeOfferId.");
        }

        return HandleRemoveTradeOffer(
            sessionId,
            playerId,
            tradeOfferId,
            connectionContext,
            requireRecipient: true,
            LobbyMessageTypes.TradeDeclineResult,
            LobbyMessageTypes.TradeOfferDeclined);
    }

    private LobbyMessageHandleResult HandleCancelTradeOffer(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadTradeOfferLifecyclePayload(root, out var sessionId, out var playerId, out var tradeOfferId))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "cancel_trade_offer requires payload.sessionId, payload.playerId, and payload.tradeOfferId.");
        }

        return HandleRemoveTradeOffer(
            sessionId,
            playerId,
            tradeOfferId,
            connectionContext,
            requireRecipient: false,
            LobbyMessageTypes.TradeCancelResult,
            LobbyMessageTypes.TradeOfferCancelled);
    }

    private LobbyMessageHandleResult HandleRemoveTradeOffer(
        string sessionId,
        string playerId,
        string tradeOfferId,
        LobbyConnectionContext connectionContext,
        bool requireRecipient,
        string directResponseType,
        string broadcastType)
    {
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

            var offer = FindPendingTradeOffer(session, tradeOfferId);
            if (offer is null)
            {
                return CreateError(
                    LobbyErrorCodes.TradeOfferNotFound,
                    "Trade offer was not found.");
            }

            var requesterId = new PlayerId(playerId);
            var authorized = requireRecipient
                ? offer.RecipientPlayerId == requesterId
                : offer.ProposerPlayerId == requesterId;
            if (!authorized)
            {
                return CreateError(
                    LobbyErrorCodes.TradeOfferNotForPlayer,
                    requireRecipient
                        ? "Only the trade offer recipient can decline it."
                        : "Only the trade offer proposer can cancel it.");
            }

            var persistence = sessionManager.RemovePendingTradeOfferAndAllocateEventSequence(
                sessionId,
                tradeOfferId);

            return CreateBroadcastResult(
                CreateTradeRemoveResult(directResponseType, offer),
                broadcastType,
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

            if (!session.GameState.Players.Any(gamePlayer => gamePlayer.PlayerId.Value == playerId))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotFound,
                    "Player is not in the game.");
            }

            return CreateSnapshotResult(session);
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

            if (escapeUse.Kind == LockupEscapeUseResultKind.EscapeCardsDisabled)
            {
                return CreateError(
                    LobbyErrorCodes.HeldCardsDisabled,
                    "Lockup escape cards are disabled by the current rules.");
            }

            var persistence = sessionManager.UpdateGameStateAndAllocateEventSequence(
                sessionId,
                escapeUse.GameState with { SuppressDoublesExtraTurnThisTurn = true });
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

    private LobbyMessageHandleResult HandleSetRules(
        JsonElement root,
        LobbyConnectionContext connectionContext)
    {
        if (!TryReadSetRulesPayload(root, out var sessionId, out var playerId, out var rulesPayload))
        {
            return CreateError(
                LobbyErrorCodes.InvalidPayload,
                "set_rules requires payload.sessionId, payload.playerId, and object payload.rules.");
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

            if (session.Status != GameSessionStatus.Lobby)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidSessionStatus,
                    "Session is not in lobby status.");
            }

            if (session.Players.All(player => player.PlayerId.Value != playerId))
            {
                return CreateError(
                    LobbyErrorCodes.PlayerNotInLobby,
                    "Player is not in lobby.");
            }

            GameRules resolvedRules;
            try
            {
                resolvedRules = GameRulesResolver.Resolve(rulesPayload);
            }
            catch (GameRulesValidationException exception)
            {
                return CreateError(
                    LobbyErrorCodes.InvalidRules,
                    exception.Message);
            }

            try
            {
                var updatedSession = sessionManager.SetDraftRules(
                    sessionId,
                    new PlayerId(playerId),
                    resolvedRules);

                return CreateRulesUpdatedBroadcastResult(updatedSession);
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
        var auctionStartedAtUtc = DateTimeOffset.UtcNow;
        var config = AuctionConfig.FromRules(gameState.Rules.Auction);
        var auctionStart = AuctionManager.StartMandatoryAuction(
            gameState,
            resolution.PlayerId,
            resolution.TileId,
            config,
            startedAtUtc: auctionStartedAtUtc);

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
            ScheduleAuctionTimer(sessionId, persistedAuctionState);

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

        var rentAssessment = PropertyManager.AssessRentForCurrentTile(gameState, resolution.PlayerId);
        var liquidation = rentAssessment.PaymentRequired && rentAssessment.OwnerId is not null
            ? LiquidationExecutionManager.ExecutePaymentObligation(
                gameState,
                new PaymentObligation(
                    rentAssessment.LandingPlayerId,
                    rentAssessment.RentDue,
                    PaymentObligationKind.Rent,
                    PaymentObligationCreditor.ForPlayer(rentAssessment.OwnerId.Value),
                    rentAssessment.TileId),
                LiquidationExecutionContext.TileExecutionPayment)
            : null;
        var rent = CreateRentPaymentResult(gameState, rentAssessment, liquidation);
        var rentGameState = rent.GameState with
        {
            HasExecutedTileThisTurn = true,
        };

        var rentPersistence = sessionManager.UpdateTerminalGameStateAndAllocateEventSequences(
            sessionId,
            rentGameState,
            DateTimeOffset.UtcNow);

        var directResponse = CreateExecuteTileResult(
                resolution,
                GetRentExecutionKind(rent),
                rentPersistence.Session.GameState,
                auction: null,
                rent: CreateRentPayload(rent, rentPersistence.Session.GameState),
                card: null,
                moneyDeltas: liquidation?.PaymentExecuted == true
                    ? CreateLiquidationMoneyDeltas(liquidation, "rent", rentPersistence.Session.GameState)
                    : CreateRentMoneyDeltas(rent, rentPersistence.Session.GameState),
                playerEliminations: CreateRentPlayerEliminations(rent, rentPersistence.Session.GameState),
                liquidationSteps: CreateLiquidationStepPayloads(liquidation));
        var result = CreateTerminalBroadcastResult(
            directResponse,
            LobbyMessageTypes.TileExecuted,
            rentPersistence.Session,
            rentPersistence);

        EmitRentStats(rent);
        EmitGameWonStat(rentPersistence, rentPersistence.Session.GameState);

        return result;
    }

    private LobbyMessageHandleResult ExecuteTaxTile(
        string sessionId,
        GameState gameState,
        TileResolutionResult resolution)
    {
        var taxAmount = new Money(gameState.Rules.Economy.IncomeTaxAmount);
        var liquidation = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            new PaymentObligation(
                resolution.PlayerId,
                taxAmount,
                PaymentObligationKind.Tax,
                PaymentObligationCreditor.Bank,
                resolution.TileId),
            LiquidationExecutionContext.TileExecutionPayment);
        var failedTaxElimination = liquidation.PaymentExecuted
            ? null
            : BankruptcyManager.EliminateForFailedPayment(gameState, resolution.PlayerId, taxAmount);
        var eliminatedGameState = liquidation.PaymentExecuted
            ? liquidation.GameState
            : failedTaxElimination?.GameState
                ?? throw new InvalidOperationException("Failed tax liquidation must produce an elimination result.");
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

        var directResponse = CreateExecuteTileResult(
                resolution,
                executionKind,
                persistence.Session.GameState,
                auction: null,
                rent: null,
                card: null,
                moneyDeltas: liquidation.PaymentExecuted
                    ? CreateLiquidationMoneyDeltas(liquidation, "tax", persistence.Session.GameState)
                    : null,
                playerEliminations: liquidation.PaymentExecuted
                    ? CreatePlayerEliminationsFromDiff(
                        gameState,
                        persistence.Session.GameState,
                        "tax",
                        taxAmount.Amount)
                    : CreatePlayerEliminationPayloads(failedTaxElimination),
                liquidationSteps: CreateLiquidationStepPayloads(liquidation));
        var result = CreateTerminalBroadcastResult(
            directResponse,
            LobbyMessageTypes.TileExecuted,
            persistence.Session,
            persistence);

        EmitGameWonStat(persistence, persistence.Session.GameState);

        return result;
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
                card: null,
                movement: CreateDirectMovementPayload(gameState, persistence.Session.GameState, resolution.PlayerId, "direct")),
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

        if (!gameState.Rules.Cards.IsDeckEnabled(deckId))
        {
            var updatedGameState = gameState with
            {
                HasExecutedTileThisTurn = true,
            };

            var disabledDeckPersistence = sessionManager.UpdateGameStateAndAllocateEventSequence(sessionId, updatedGameState);

            return CreateBroadcastResult(
                CreateExecuteTileResult(
                    tileResolution,
                    "no_action",
                    disabledDeckPersistence.Session.GameState,
                    auction: null,
                    rent: null,
                    card: null),
                LobbyMessageTypes.TileExecuted,
                disabledDeckPersistence.Session,
                disabledDeckPersistence.Sequence);
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

        var cardObligation = CardEffectExecutor.CreateSingleDebtorBankPaymentObligation(gameState, cardResolution);
        var multiCreditorObligation = CardEffectExecutor.CreateMultiCreditorPaymentObligation(cardResolution);
        var liquidation = cardObligation is null
            ? null
            : LiquidationExecutionManager.ExecutePaymentObligation(
                gameState,
                cardObligation,
                LiquidationExecutionContext.TileExecutionPayment);
        var multiCreditorLiquidation = multiCreditorObligation is null
            ? null
            : LiquidationExecutionManager.ExecuteMultiCreditorPaymentObligation(
                gameState,
                multiCreditorObligation,
                LiquidationExecutionContext.TileExecutionPayment);
        CardEffectExecutionResult executionResult;
        if (multiCreditorLiquidation?.PaymentExecuted == true)
        {
            executionResult = new CardEffectExecutionResult(multiCreditorLiquidation.GameState);
        }
        else if (multiCreditorLiquidation?.ResultKind == LiquidationExecutionResultKind.Insolvent)
        {
            executionResult = new CardEffectExecutionResult(
                BankruptcyManager.EliminateForFailedPayment(
                    gameState,
                    multiCreditorLiquidation.Obligation.DebtorPlayerId,
                    multiCreditorLiquidation.AmountDue).GameState);
        }
        else if (liquidation?.PaymentExecuted == true)
        {
            executionResult = new CardEffectExecutionResult(liquidation.GameState);
        }
        else if (liquidation?.ResultKind == LiquidationExecutionResultKind.Insolvent)
        {
            executionResult = new CardEffectExecutionResult(
                BankruptcyManager.EliminateForFailedPayment(
                    gameState,
                    liquidation.Obligation.DebtorPlayerId,
                    liquidation.Obligation.Amount).GameState);
        }
        else
        {
            executionResult = CardEffectExecutor.ExecuteCardEffectWithResult(gameState, cardResolution);
        }

        var executedGameState = executionResult.GameState;
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
        var executionKind = GetCardExecutionKind(cardResolution, persistedPlayer);

        var directResponse = CreateExecuteTileResult(
                tileResolution,
                executionKind,
                persistence.Session.GameState,
                auction: null,
                rent: null,
                card: CreateCardPayload(deckId, card, cardResolution, executionKind, persistedPlayer),
                movement: CreateCardMovementPayload(
                    gameState,
                    persistence.Session.GameState,
                    player.PlayerId,
                    cardResolution,
                    executionResult.MovementResult),
                moneyDeltas: liquidation?.PaymentExecuted == true
                    ? CreateLiquidationMoneyDeltas(liquidation, "card", persistence.Session.GameState)
                    : multiCreditorLiquidation?.PaymentExecuted == true
                        ? CreateLiquidationMoneyDeltas(multiCreditorLiquidation, "card", persistence.Session.GameState)
                    : CreateMoneyDeltasFromDiff(
                        gameState,
                        persistence.Session.GameState,
                        "card",
                        cardId: card.CardId),
                playerEliminations: CreatePlayerEliminationsFromDiff(
                    gameState,
                    persistence.Session.GameState,
                    "card_payment",
                    GetCardPaymentDue(cardObligation, multiCreditorLiquidation)),
                liquidationSteps: liquidation?.PaymentExecuted == true
                    ? CreateLiquidationStepPayloads(liquidation)
                    : CreateLiquidationStepPayloads(multiCreditorLiquidation));
        var result = CreateTerminalBroadcastResult(
            directResponse,
            LobbyMessageTypes.TileExecuted,
            persistence.Session,
            persistence);

        EmitCardStats(gameState, persistence.Session.GameState, tileResolution, player, cardResolution);
        EmitGameWonStat(persistence, persistence.Session.GameState);

        return result;
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

    private static bool TryReadCreateTradeOfferPayload(
        JsonElement root,
        out string sessionId,
        out string playerId,
        out string recipientPlayerId,
        out TradeAssets offered,
        out TradeAssets requested)
    {
        sessionId = string.Empty;
        playerId = string.Empty;
        recipientPlayerId = string.Empty;
        offered = new TradeAssets(Money.Zero, Array.Empty<TileId>());
        requested = new TradeAssets(Money.Zero, Array.Empty<TileId>());

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !TryReadString(payload, "sessionId", out sessionId) ||
            !TryReadString(payload, "playerId", out playerId) ||
            !TryReadString(payload, "recipientPlayerId", out recipientPlayerId) ||
            !payload.TryGetProperty("offered", out var offeredProperty) ||
            !payload.TryGetProperty("requested", out var requestedProperty) ||
            !TryReadTradeAssets(offeredProperty, out offered) ||
            !TryReadTradeAssets(requestedProperty, out requested))
        {
            return false;
        }

        return true;
    }

    private static bool TryReadTradeOfferLifecyclePayload(
        JsonElement root,
        out string sessionId,
        out string playerId,
        out string tradeOfferId)
    {
        sessionId = string.Empty;
        playerId = string.Empty;
        tradeOfferId = string.Empty;

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !TryReadString(payload, "sessionId", out sessionId) ||
            !TryReadString(payload, "playerId", out playerId) ||
            !TryReadString(payload, "tradeOfferId", out tradeOfferId))
        {
            return false;
        }

        return true;
    }

    private static bool TryReadTradeAssets(JsonElement element, out TradeAssets assets)
    {
        assets = new TradeAssets(Money.Zero, Array.Empty<TileId>());

        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("cash", out var cashProperty) ||
            cashProperty.ValueKind != JsonValueKind.Number ||
            !cashProperty.TryGetInt32(out var cash) ||
            cash < 0 ||
            !element.TryGetProperty("propertyTileIds", out var propertyTileIdsProperty) ||
            propertyTileIdsProperty.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var propertyTileIds = new List<TileId>();
        foreach (var propertyTileId in propertyTileIdsProperty.EnumerateArray())
        {
            if (propertyTileId.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(propertyTileId.GetString()))
            {
                return false;
            }

            propertyTileIds.Add(new TileId(propertyTileId.GetString()!));
        }

        assets = new TradeAssets(new Money(cash), propertyTileIds);
        return true;
    }

    private static bool TryReadPropertyMortgagePayload(
        JsonElement root,
        out string sessionId,
        out string playerId,
        out string propertyTileId)
    {
        sessionId = string.Empty;
        playerId = string.Empty;
        propertyTileId = string.Empty;

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !TryReadString(payload, "sessionId", out sessionId) ||
            !TryReadString(payload, "playerId", out playerId) ||
            !TryReadString(payload, "propertyTileId", out propertyTileId))
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

    private static bool TryReadSetRulesPayload(
        JsonElement root,
        out string sessionId,
        out string playerId,
        out JsonElement rules)
    {
        sessionId = string.Empty;
        playerId = string.Empty;
        rules = default;

        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !TryReadString(payload, "sessionId", out sessionId) ||
            !TryReadString(payload, "playerId", out playerId) ||
            !payload.TryGetProperty("rules", out rules) ||
            rules.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

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
                    .ToArray(),
                session.DraftRules));
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

    private static LobbyServerEnvelope CreateSnapshotResult(GameSession session)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.SnapshotResult,
            CreateSnapshotPayload(session));
    }

    private static LobbyServerEnvelope CreateReconnectResult(GameSession session, string playerId)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.ReconnectResult,
            new ReconnectResultPayload(
                session.SessionId,
                playerId,
                session.LastEventSequence,
                CreateSnapshotPayload(session)));
    }

    private static SnapshotPayload CreateSnapshotPayload(GameSession session)
    {
        var gameState = session.GameState;
        return new SnapshotPayload(
            SnapshotVersion: 1,
            SessionId: gameState.MatchId.Value,
            Status: "in_game",
            GameStatus: FormatGameStatus(gameState.Status),
            ServerNowUtc: DateTimeOffset.UtcNow,
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
            PropertyStates: gameState.PropertyStates
                .Where(propertyState =>
                    propertyState.Value.Data.DamagePercent > 0 ||
                    propertyState.Value.Data.IsMortgaged ||
                    propertyState.Value.Data.UpgradeLevel > 0)
                .OrderBy(propertyState => propertyState.Value.TileId.Value, StringComparer.Ordinal)
                .Select(propertyState => CreateSnapshotPropertyState(propertyState.Value))
                .ToArray(),
            ActiveAuction: gameState.ActiveAuctionState is null
                ? null
                : CreateSnapshotAuction(gameState.ActiveAuctionState),
            CardDecks: gameState.CardDeckStates
                .OrderBy(deckState => deckState.Value.DeckId, StringComparer.Ordinal)
                .Select(deckState => CreateSnapshotCardDeck(deckState.Value))
                .ToArray(),
            LoanShark: new SnapshotLoanSharkPayload(
                LoanSharkConfig.FromRules(gameState.Rules.Loans).Enabled),
            Rules: gameState.Rules,
            PendingTrades: gameState.Status == GameStatus.Completed
                ? Array.Empty<PendingTradePayload>()
                : session.PendingTradeOffers
                    .OrderBy(offer => offer.CreatedSequence)
                    .Select(CreatePendingTradePayload)
                    .ToArray());
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
            player.StatusEffects
                .Select(CreateSnapshotStatusEffect)
                .ToArray(),
            CreateSnapshotLoan(player.LoanState),
            player.TurnState.JailTurnCount,
            player.TurnState.JailRollAttemptCount,
            player.TurnState.ConsecutiveDoublesCount,
            player.TurnState.LastJailReleaseReason,
            player.IsBankrupt,
            player.IsEliminated,
            player.IsLockedUp);
    }

    private static SnapshotPlayerStatusEffectPayload CreateSnapshotStatusEffect(PlayerStatusEffect statusEffect)
    {
        return new SnapshotPlayerStatusEffectPayload(
            statusEffect.InstanceId,
            FormatPlayerStatusEffectKind(statusEffect.Kind),
            new SnapshotPlayerStatusEffectDataPayload(
                statusEffect.Data.DefinitionId,
                statusEffect.Data.StackCount,
                statusEffect.Data.RemainingTurns,
                statusEffect.Data.SourceId));
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

    private static SnapshotPropertyStatePayload CreateSnapshotPropertyState(PropertyState propertyState)
    {
        return new SnapshotPropertyStatePayload(
            propertyState.TileId.Value,
            new SnapshotPropertyStateDataPayload(
                propertyState.Data.DamagePercent,
                propertyState.Data.IsMortgaged,
                propertyState.Data.UpgradeLevel));
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
            auctionState.TimerEndsAtUtc,
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

    private static PendingTradePayload CreatePendingTradePayload(PendingTradeOffer offer)
    {
        return new PendingTradePayload(
            offer.TradeOfferId,
            offer.CreatedSequence,
            offer.ProposerPlayerId.Value,
            offer.RecipientPlayerId.Value,
            CreateTradeAssetsPayload(offer.Offered),
            CreateTradeAssetsPayload(offer.Requested),
            offer.CreatedAtUtc);
    }

    private static TradeAssetsPayload CreateTradeAssetsPayload(TradeAssets assets)
    {
        return new TradeAssetsPayload(
            assets.Cash.Amount,
            assets.PropertyTileIds
                .Select(tileId => tileId.Value)
                .OrderBy(tileId => tileId, StringComparer.Ordinal)
                .ToArray());
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

    private static LobbyMessageHandleResult CreateRulesUpdatedBroadcastResult(GameSession session)
    {
        var directResponse = new LobbyServerEnvelope(
            LobbyMessageTypes.RulesUpdated,
            new RulesUpdatedPayload(session.SessionId, session.DraftRules));

        return new LobbyMessageHandleResult(
            directResponse,
            new LobbyBroadcastEnvelope(
                LobbyMessageTypes.RulesUpdated,
                session.LastEventSequence,
                session.SessionId,
                session.GameState.MatchId.Value,
                DateTimeOffset.UtcNow,
                directResponse.Payload),
            CreateLobbyBroadcastTargetConnectionIds(session));
    }

    private void ScheduleAuctionTimer(string sessionId, AuctionState auctionState)
    {
        if (auctionState.TimerEndsAtUtc is null)
        {
            throw new InvalidOperationException("Active auction timers must persist a deadline.");
        }

        auctionTimerService.Schedule(sessionId, auctionState.TimerEndsAtUtc.Value);
    }

    private void EmitRentStats(RentPaymentResult rent)
    {
        if (!rent.RentCharged)
        {
            return;
        }

        EmitStatEvent(new StatEvent(
            rent.LandingPlayerId,
            StatEventKind.RentPaid,
            rent.RentPaid,
            rent.TileId,
            "rent"));

        if (rent.OwnerId is not null)
        {
            EmitStatEvent(new StatEvent(
                rent.OwnerId.Value,
                StatEventKind.RentReceived,
                rent.RentPaid,
                rent.TileId,
                "rent"));
        }
    }

    private void EmitAuctionStats(AuctionFinalizationResult finalizationResult)
    {
        if (!finalizationResult.FinalizedWithWinner ||
            finalizationResult.WinnerId is null ||
            finalizationResult.WinningBid is null)
        {
            return;
        }

        EmitStatEvent(new StatEvent(
            finalizationResult.WinnerId.Value,
            StatEventKind.AuctionWon,
            finalizationResult.WinningBid.Value,
            finalizationResult.PropertyTileId,
            "auction"));
    }

    private void EmitCardStats(
        GameState previousGameState,
        GameState persistedGameState,
        TileResolutionResult tileResolution,
        Player previousPlayer,
        CardResolutionResult cardResolution)
    {
        EmitStatEvent(new StatEvent(
            previousPlayer.PlayerId,
            StatEventKind.CardTriggered,
            TileId: tileResolution.TileId,
            Source: "card"));

        var persistedPlayer = persistedGameState.Players.First(player => player.PlayerId == previousPlayer.PlayerId);
        if (cardResolution.ActionKind == CardResolutionActionKind.ApplySlimer &&
            !PlayerStatusEffectManager.HasSlimer(previousPlayer) &&
            PlayerStatusEffectManager.HasSlimer(persistedPlayer))
        {
            EmitStatEvent(new StatEvent(
                previousPlayer.PlayerId,
                StatEventKind.SlimerApplied,
                TileId: tileResolution.TileId,
                Source: "card"));
        }

        if (cardResolution.ActionKind != CardResolutionActionKind.ApplyEarthquake)
        {
            return;
        }

        var targetTileIds = cardResolution.Parameters?.TileIds?
            .Distinct()
            .OrderBy(tileId => tileId.Value, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<TileId>();
        foreach (var tileId in targetTileIds)
        {
            if (GetPropertyDamagePercent(persistedGameState, tileId) <= GetPropertyDamagePercent(previousGameState, tileId))
            {
                continue;
            }

            EmitStatEvent(new StatEvent(
                previousPlayer.PlayerId,
                StatEventKind.EarthquakeApplied,
                TileId: tileId,
                Source: "card"));
        }
    }

    private void EmitPropertyRepairStats(GameState previousGameState, GameState persistedGameState)
    {
        if (persistedGameState.CurrentTurnPlayerId is null)
        {
            return;
        }

        var player = persistedGameState.Players.FirstOrDefault(candidate =>
            candidate.PlayerId == persistedGameState.CurrentTurnPlayerId.Value);
        if (player is null)
        {
            return;
        }

        foreach (var tileId in player.OwnedPropertyIds.OrderBy(tileId => tileId.Value, StringComparer.Ordinal))
        {
            var previousDamagePercent = GetPropertyDamagePercent(previousGameState, tileId);
            var persistedDamagePercent = GetPropertyDamagePercent(persistedGameState, tileId);
            if (persistedDamagePercent >= previousDamagePercent)
            {
                continue;
            }

            EmitStatEvent(new StatEvent(
                player.PlayerId,
                StatEventKind.PropertyRepaired,
                CalculateRepairCost(previousGameState, tileId, previousDamagePercent - persistedDamagePercent),
                tileId,
                "property_repair"));
        }
    }

    private void EmitGameWonStat(GameStateEventPersistenceResult persistence, GameState persistedGameState)
    {
        if (persistence.CompletionSequence is null || persistedGameState.WinnerPlayerId is null)
        {
            return;
        }

        EmitStatEvent(new StatEvent(
            persistedGameState.WinnerPlayerId.Value,
            StatEventKind.GameWon,
            Source: "completion"));
    }

    private void EmitStatEvent(StatEvent evt)
    {
        try
        {
            statEventSink.Emit(evt);
        }
        catch
        {
        }
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
        GameState previousGameState,
        GameState gameState,
        MovementPayload? movement,
        IReadOnlyList<MoneyDeltaPayload>? moneyDeltas,
        string rollKind)
    {
        var persistedPlayer = gameState.Players.First(player => player.PlayerId == playerId);

        return new LobbyServerEnvelope(
            LobbyMessageTypes.RollResult,
            new RollResultPayload(
                persistedPlayer.PlayerId.Value,
                new[] { dice.FirstDie, dice.SecondDie },
                dice.Total,
                dice.IsDouble,
                persistedPlayer.CurrentTileId.Value,
                movement?.PassedStart ?? false,
                gameState.HasRolledThisTurn,
                movement,
                moneyDeltas,
                rollKind,
                persistedPlayer.IsLockedUp
                    ? persistedPlayer.TurnState.JailRollAttemptCount
                    : null,
                CreatePlayerEliminationsFromDiff(previousGameState, gameState, "fine")));
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
        ExecuteTileCardPayload? card,
        MovementPayload? movement = null,
        IReadOnlyList<MoneyDeltaPayload>? moneyDeltas = null,
        IReadOnlyList<PropertyOwnershipChangePayload>? propertyOwnershipChanges = null,
        IReadOnlyList<PlayerEliminationPayload>? playerEliminations = null,
        IReadOnlyList<LiquidationStepPayload>? liquidationSteps = null)
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
                card,
                movement,
                moneyDeltas,
                propertyOwnershipChanges,
                playerEliminations,
                liquidationSteps));
    }

    private static LobbyServerEnvelope CreateEndTurnResult(
        PlayerId previousPlayerId,
        GameState previousGameState,
        GameState gameState)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.EndTurnResult,
            new EndTurnResultPayload(
                previousPlayerId.Value,
                gameState.CurrentTurnPlayerId?.Value,
                gameState.TurnNumber,
                CreateEndTurnMoneyDeltas(previousGameState, gameState),
                CreatePlayerEliminationsFromDiff(previousGameState, gameState, "negative_balance")));
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
                    ?? throw new InvalidOperationException("Accepted auction bids must have a highest bidder."),
                auctionState.PropertyTileId.Value,
                FormatAuctionStatus(auctionState.Status),
                (auctionState.HighestBid?.Amount
                    ?? throw new InvalidOperationException("Accepted auction bids must have a highest bid.")) +
                    auctionState.MinimumBidIncrement.Amount,
                auctionState.Bids.Count,
                auctionState.CountdownDurationSeconds,
                auctionState.TimerEndsAtUtc));
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
                tileId.Value,
                CreateAuctionMoneyDeltas(finalizationResult, persistedGameState),
                CreateAuctionOwnershipChanges(finalizationResult),
                CreateAuctionPlayerEliminations(finalizationResult, persistedGameState),
                CreateLiquidationStepPayloads(finalizationResult.PaymentLiquidation)));
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
                loanState.LoanTier,
                new[]
                {
                    new MoneyDeltaPayload(
                        persistedPlayer.PlayerId.Value,
                        amount,
                        persistedPlayer.Money.Amount,
                        "loan"),
                }));
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

    private static LobbyServerEnvelope CreateTradeOfferResult(PendingTradeOffer offer)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.TradeOfferResult,
            new TradeOfferResultPayload(CreatePendingTradePayload(offer)));
    }

    private static LobbyServerEnvelope CreateTradeAcceptResult(
        PendingTradeOffer offer,
        GameState previousGameState,
        GameState gameState,
        TradeSettlementResult settlement)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.TradeAcceptResult,
            new TradeAcceptResultPayload(
                offer.TradeOfferId,
                offer.ProposerPlayerId.Value,
                offer.RecipientPlayerId.Value,
                CreateMoneyDeltasFromDiff(previousGameState, gameState, "trade"),
                CreateTradeOwnershipChanges(settlement)));
    }

    private static LobbyServerEnvelope CreateTradeRemoveResult(
        string responseType,
        PendingTradeOffer offer)
    {
        var payload = responseType == LobbyMessageTypes.TradeDeclineResult
            ? new TradeDeclineResultPayload(
                offer.TradeOfferId,
                offer.ProposerPlayerId.Value,
                offer.RecipientPlayerId.Value)
            : (object)new TradeCancelResultPayload(
                offer.TradeOfferId,
                offer.ProposerPlayerId.Value,
                offer.RecipientPlayerId.Value);

        return new LobbyServerEnvelope(responseType, payload);
    }

    private static LobbyServerEnvelope CreateMortgageResult(MortgageResult mortgageResult)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.MortgageResult,
            new MortgageResultPayload(
                mortgageResult.PlayerId.Value,
                mortgageResult.PropertyTileId.Value,
                mortgageResult.MortgageValue.Amount,
                mortgageResult.Money.Amount,
                mortgageResult.IsMortgaged,
                new[]
                {
                    new MoneyDeltaPayload(
                        mortgageResult.PlayerId.Value,
                        mortgageResult.MortgageValue.Amount,
                        mortgageResult.Money.Amount,
                        "mortgage",
                        TileId: mortgageResult.PropertyTileId.Value),
                }));
    }

    private static LobbyServerEnvelope CreateUnmortgageResult(UnmortgageResult unmortgageResult)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.UnmortgageResult,
            new UnmortgageResultPayload(
                unmortgageResult.PlayerId.Value,
                unmortgageResult.PropertyTileId.Value,
                unmortgageResult.MortgageValue.Amount,
                unmortgageResult.UnmortgageInterest.Amount,
                unmortgageResult.UnmortgageCost.Amount,
                unmortgageResult.Money.Amount,
                unmortgageResult.IsMortgaged,
                new[]
                {
                    new MoneyDeltaPayload(
                        unmortgageResult.PlayerId.Value,
                        -unmortgageResult.UnmortgageCost.Amount,
                        unmortgageResult.Money.Amount,
                        "unmortgage",
                        TileId: unmortgageResult.PropertyTileId.Value),
                }));
    }

    private static LobbyServerEnvelope CreateUpgradeResult(PropertyUpgradeResult upgradeResult)
    {
        return new LobbyServerEnvelope(
            LobbyMessageTypes.UpgradeResult,
            new UpgradeResultPayload(
                upgradeResult.PlayerId.Value,
                upgradeResult.PropertyTileId.Value,
                upgradeResult.UpgradeLevel,
                upgradeResult.UpgradeCost.Amount,
                upgradeResult.Money.Amount,
                new[]
                {
                    new MoneyDeltaPayload(
                        upgradeResult.PlayerId.Value,
                        -upgradeResult.UpgradeCost.Amount,
                        upgradeResult.Money.Amount,
                        "property_upgrade",
                        TileId: upgradeResult.PropertyTileId.Value),
                }));
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
            AuctionBidResultKind.BidderCannotCoverBid => CreateError(
                LobbyErrorCodes.InsufficientCash,
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
            LoanTakeResultKind.LoanModeDisabled => CreateError(
                LobbyErrorCodes.LoanModeDisabled,
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

    private static LobbyServerEnvelope CreateMortgageRejectedError(MortgageResult mortgageResult)
    {
        return mortgageResult.ResultKind switch
        {
            MortgageResultKind.MortgagesDisabled => CreateError(
                LobbyErrorCodes.MortgageModeDisabled,
                mortgageResult.Message),
            MortgageResultKind.PlayerNotInGame => CreateError(
                LobbyErrorCodes.PlayerNotFound,
                mortgageResult.Message),
            MortgageResultKind.PlayerEliminated => CreateError(
                LobbyErrorCodes.PlayerEliminated,
                mortgageResult.Message),
            MortgageResultKind.InvalidProperty => CreateError(
                LobbyErrorCodes.InvalidPayload,
                mortgageResult.Message),
            MortgageResultKind.PropertyNotOwned => CreateError(
                LobbyErrorCodes.PropertyNotOwned,
                mortgageResult.Message),
            MortgageResultKind.AlreadyMortgaged => CreateError(
                LobbyErrorCodes.PropertyAlreadyMortgaged,
                mortgageResult.Message),
            _ => CreateError(
                LobbyErrorCodes.InvalidSessionState,
                mortgageResult.Message),
        };
    }

    private static LobbyServerEnvelope CreateUnmortgageRejectedError(UnmortgageResult unmortgageResult)
    {
        return unmortgageResult.ResultKind switch
        {
            UnmortgageResultKind.MortgagesDisabled => CreateError(
                LobbyErrorCodes.MortgageModeDisabled,
                unmortgageResult.Message),
            UnmortgageResultKind.PlayerNotInGame => CreateError(
                LobbyErrorCodes.PlayerNotFound,
                unmortgageResult.Message),
            UnmortgageResultKind.PlayerEliminated => CreateError(
                LobbyErrorCodes.PlayerEliminated,
                unmortgageResult.Message),
            UnmortgageResultKind.InvalidProperty => CreateError(
                LobbyErrorCodes.InvalidPayload,
                unmortgageResult.Message),
            UnmortgageResultKind.PropertyNotOwned => CreateError(
                LobbyErrorCodes.PropertyNotOwned,
                unmortgageResult.Message),
            UnmortgageResultKind.NotMortgaged => CreateError(
                LobbyErrorCodes.PropertyNotMortgaged,
                unmortgageResult.Message),
            UnmortgageResultKind.InsufficientCash => CreateError(
                LobbyErrorCodes.InsufficientCash,
                unmortgageResult.Message),
            _ => CreateError(
                LobbyErrorCodes.InvalidSessionState,
                unmortgageResult.Message),
        };
    }

    private static LobbyServerEnvelope CreateUpgradeRejectedError(
        PropertyUpgradeResult upgradeResult,
        GameStatus gameStatus)
    {
        return upgradeResult.ResultKind switch
        {
            PropertyUpgradeResultKind.UpgradesDisabled => CreateError(
                LobbyErrorCodes.UpgradeModeDisabled,
                upgradeResult.Message),
            PropertyUpgradeResultKind.PlayerNotInGame => CreateError(
                LobbyErrorCodes.PlayerNotFound,
                upgradeResult.Message),
            PropertyUpgradeResultKind.PlayerBankrupt or
                PropertyUpgradeResultKind.PlayerEliminated => CreateError(
                LobbyErrorCodes.PlayerEliminated,
                upgradeResult.Message),
            PropertyUpgradeResultKind.GameNotInProgress => gameStatus == GameStatus.Completed
                ? CreateGameAlreadyCompletedError()
                : CreateError(
                    LobbyErrorCodes.InvalidSessionState,
                    upgradeResult.Message),
            PropertyUpgradeResultKind.InvalidProperty or
                PropertyUpgradeResultKind.IncompleteGroupOwnership or
                PropertyUpgradeResultKind.RentTierUnavailable => CreateError(
                LobbyErrorCodes.InvalidPayload,
                upgradeResult.Message),
            PropertyUpgradeResultKind.PropertyNotOwned => CreateError(
                LobbyErrorCodes.PropertyNotOwned,
                upgradeResult.Message),
            PropertyUpgradeResultKind.InsufficientCash or
                PropertyUpgradeResultKind.UnsafeMoneyBalance => CreateError(
                LobbyErrorCodes.InsufficientCash,
                upgradeResult.Message),
            _ => CreateError(
                LobbyErrorCodes.InvalidSessionState,
                upgradeResult.Message),
        };
    }

    private static LobbyServerEnvelope CreateTradeValidationRejectedError(TradeValidationResult validation)
    {
        return validation.ResultKind switch
        {
            TradeSettlementResultKind.GameNotInProgress => CreateGameAlreadyCompletedError(),
            TradeSettlementResultKind.ActiveAuction or
                TradeSettlementResultKind.UnresolvedTileExecution => CreateError(
                LobbyErrorCodes.InvalidSessionState,
                validation.Message),
            TradeSettlementResultKind.PlayerNotInGame => CreateError(
                LobbyErrorCodes.PlayerNotFound,
                validation.Message),
            TradeSettlementResultKind.PlayerBankrupt or
                TradeSettlementResultKind.PlayerEliminated => CreateError(
                LobbyErrorCodes.PlayerEliminated,
                validation.Message),
            TradeSettlementResultKind.PropertyNotOwnedByOfferingPlayer => CreateError(
                LobbyErrorCodes.PropertyNotOwned,
                validation.Message),
            TradeSettlementResultKind.InsufficientCash or
                TradeSettlementResultKind.UnsafeMoneyBalance => CreateError(
                LobbyErrorCodes.InsufficientCash,
                validation.Message),
            _ => CreateError(
                LobbyErrorCodes.InvalidPayload,
                validation.Message),
        };
    }

    private static LobbyServerEnvelope CreateTradeSettlementRejectedError(TradeSettlementResult settlement)
    {
        return settlement.ResultKind switch
        {
            TradeSettlementResultKind.GameNotInProgress => CreateGameAlreadyCompletedError(),
            TradeSettlementResultKind.ActiveAuction or
                TradeSettlementResultKind.UnresolvedTileExecution => CreateError(
                LobbyErrorCodes.InvalidSessionState,
                settlement.Message),
            TradeSettlementResultKind.PlayerNotInGame => CreateError(
                LobbyErrorCodes.PlayerNotFound,
                settlement.Message),
            TradeSettlementResultKind.PlayerBankrupt or
                TradeSettlementResultKind.PlayerEliminated => CreateError(
                LobbyErrorCodes.PlayerEliminated,
                settlement.Message),
            TradeSettlementResultKind.PropertyNotOwnedByOfferingPlayer => CreateError(
                LobbyErrorCodes.PropertyNotOwned,
                settlement.Message),
            TradeSettlementResultKind.InsufficientCash or
                TradeSettlementResultKind.UnsafeMoneyBalance => CreateError(
                LobbyErrorCodes.InsufficientCash,
                settlement.Message),
            _ => CreateError(
                LobbyErrorCodes.InvalidPayload,
                settlement.Message),
        };
    }

    private static LobbyServerEnvelope CreateGameAlreadyCompletedError()
    {
        return CreateError(
            LobbyErrorCodes.GameAlreadyCompleted,
            "Game is already completed.");
    }

    private static MovementPayload CreateMovementPayload(MovementResult movementResult)
    {
        return new MovementPayload(
            movementResult.PlayerId.Value,
            movementResult.FromTileId.Value,
            movementResult.ToTileId.Value,
            movementResult.PathTileIds.Select(tileId => tileId.Value).ToArray(),
            movementResult.StepCount,
            movementResult.MovementKind,
            movementResult.PassedStart);
    }

    private static bool ShouldSendToLockupForConsecutiveDoubles(
        GameState gameState,
        Player player,
        DiceRoll dice)
    {
        return gameState.Rules.Jail.Enabled &&
            gameState.Rules.Dice.DoublesExtraTurnEnabled &&
            dice.IsDouble &&
            player.TurnState.ConsecutiveDoublesCount >= gameState.Rules.Dice.MaxConsecutiveDoublesBeforeLockup;
    }

    private static bool ShouldGrantDoublesExtraTurn(GameState gameState, Player player)
    {
        return gameState.Rules.Dice.DoublesExtraTurnEnabled &&
            !gameState.SuppressDoublesExtraTurnThisTurn &&
            !player.IsEliminated &&
            !player.IsLockedUp &&
            player.TurnState.ConsecutiveDoublesCount > 0 &&
            player.TurnState.ConsecutiveDoublesCount < gameState.Rules.Dice.MaxConsecutiveDoublesBeforeLockup;
    }

    private static IReadOnlyList<MoneyDeltaPayload>? CreateRollMoneyDeltas(
        GameState previousGameState,
        GameState gameState,
        MovementResult? movement,
        Money? fineAmount)
    {
        var player = gameState.Players.First(updatedPlayer => updatedPlayer.PlayerId == previousGameState.CurrentTurnPlayerId);
        var deltas = new List<MoneyDeltaPayload>();

        if (fineAmount is not null)
        {
            deltas.Add(new MoneyDeltaPayload(
                player.PlayerId.Value,
                -fineAmount.Value.Amount,
                player.Money.Amount,
                "jail_fine"));
        }

        if (movement?.PassedStart == true)
        {
            deltas.Add(new MoneyDeltaPayload(
                player.PlayerId.Value,
                gameState.Rules.Economy.PassStartReward,
                player.Money.Amount,
                "pass_start"));
        }

        return deltas.Count == 0 ? null : deltas;
    }

    private static MovementPayload? CreateDirectMovementPayload(
        GameState previousGameState,
        GameState gameState,
        PlayerId playerId,
        string movementKind)
    {
        var previousPlayer = previousGameState.Players.First(player => player.PlayerId == playerId);
        var player = gameState.Players.First(updatedPlayer => updatedPlayer.PlayerId == playerId);
        if (previousPlayer.CurrentTileId == player.CurrentTileId)
        {
            return null;
        }

        return new MovementPayload(
            playerId.Value,
            previousPlayer.CurrentTileId.Value,
            player.CurrentTileId.Value,
            new[] { player.CurrentTileId.Value },
            StepCount: 0,
            MovementKind: movementKind,
            PassedStart: false);
    }

    private static MovementPayload? CreateCardMovementPayload(
        GameState previousGameState,
        GameState gameState,
        PlayerId playerId,
        CardResolutionResult cardResolution,
        MovementResult? movementResult)
    {
        if (movementResult is not null)
        {
            return CreateMovementPayload(movementResult);
        }

        return cardResolution.ActionKind == CardResolutionActionKind.GoToLockup
            ? CreateDirectMovementPayload(previousGameState, gameState, playerId, "direct")
            : null;
    }

    private static RentPaymentResult CreateRentPaymentResult(
        GameState gameState,
        RentAssessmentResult assessment,
        LiquidationExecutionResult? liquidation)
    {
        if (!assessment.PaymentRequired || assessment.OwnerId is null)
        {
            return new RentPaymentResult(
                gameState,
                assessment.LandingPlayerId,
                assessment.TileId,
                assessment.OwnerId,
                Money.Zero,
                Money.Zero);
        }

        if (liquidation?.PaymentExecuted == true)
        {
            return new RentPaymentResult(
                liquidation.GameState,
                assessment.LandingPlayerId,
                assessment.TileId,
                assessment.OwnerId,
                assessment.RentDue,
                liquidation.AmountPaid);
        }

        return PropertyManager.PayRentForCurrentTile(
            liquidation?.GameState ?? gameState,
            assessment.LandingPlayerId);
    }

    private static IReadOnlyList<MoneyDeltaPayload>? CreateRentMoneyDeltas(
        RentPaymentResult rent,
        GameState gameState)
    {
        if (rent.RentPaid.Amount == 0 || rent.OwnerId is null)
        {
            return null;
        }

        var payer = gameState.Players.First(player => player.PlayerId == rent.LandingPlayerId);
        var owner = gameState.Players.First(player => player.PlayerId == rent.OwnerId.Value);

        return new[]
        {
            new MoneyDeltaPayload(
                payer.PlayerId.Value,
                -rent.RentPaid.Amount,
                payer.Money.Amount,
                "rent",
                owner.PlayerId.Value,
                rent.TileId.Value),
            new MoneyDeltaPayload(
                owner.PlayerId.Value,
                rent.RentPaid.Amount,
                owner.Money.Amount,
                "rent",
                payer.PlayerId.Value,
                rent.TileId.Value),
        };
    }

    private static IReadOnlyList<PlayerEliminationPayload>? CreateRentPlayerEliminations(
        RentPaymentResult rent,
        GameState gameState)
    {
        if (rent.EliminationResult is null || !rent.EliminationResult.WasEliminated)
        {
            return null;
        }

        var player = gameState.Players.First(updatedPlayer => updatedPlayer.PlayerId == rent.LandingPlayerId);
        return new[]
        {
            new PlayerEliminationPayload(
                player.PlayerId.Value,
                FormatEliminationReason(rent.EliminationResult.Reason),
                player.Money.Amount,
                rent.RentDue.Amount),
        };
    }

    private static IReadOnlyList<MoneyDeltaPayload>? CreateMoneyDeltasFromDiff(
        GameState previousGameState,
        GameState gameState,
        string reason,
        TileId? tileId = null,
        CardId? cardId = null)
    {
        var deltas = new List<MoneyDeltaPayload>();
        foreach (var player in gameState.Players)
        {
            var previousPlayer = previousGameState.Players.FirstOrDefault(
                candidate => candidate.PlayerId == player.PlayerId);
            if (previousPlayer is null)
            {
                continue;
            }

            var delta = player.Money.Amount - previousPlayer.Money.Amount;
            if (delta == 0)
            {
                continue;
            }

            deltas.Add(new MoneyDeltaPayload(
                player.PlayerId.Value,
                delta,
                player.Money.Amount,
                reason,
                CounterpartyPlayerId: null,
                TileId: tileId?.Value,
                CardId: cardId?.Value));
        }

        return deltas.Count == 0 ? null : deltas;
    }

    private static IReadOnlyList<MoneyDeltaPayload>? CreateLiquidationMoneyDeltas(
        LiquidationExecutionResult liquidation,
        string paymentReason,
        GameState gameState)
    {
        if (!liquidation.PaymentExecuted)
        {
            return null;
        }

        var deltas = new List<MoneyDeltaPayload>();
        foreach (var step in liquidation.Steps)
        {
            switch (step.StepKind)
            {
                case LiquidationStepKind.UpgradeSale:
                    deltas.Add(CreateDebtorLiquidationDelta(liquidation, step, "liquidation_upgrade_sale"));
                    break;
                case LiquidationStepKind.Mortgage:
                    deltas.Add(CreateDebtorLiquidationDelta(liquidation, step, "liquidation_mortgage"));
                    break;
                case LiquidationStepKind.BankPayment:
                    deltas.Add(CreateDebtorLiquidationDelta(liquidation, step, paymentReason));
                    break;
                case LiquidationStepKind.PlayerPayment:
                    deltas.Add(CreateDebtorLiquidationDelta(liquidation, step, paymentReason));
                    if (liquidation.Obligation.Creditor.PlayerId is not null)
                    {
                        var creditor = gameState.Players.First(
                            player => player.PlayerId == liquidation.Obligation.Creditor.PlayerId.Value);
                        deltas.Add(new MoneyDeltaPayload(
                            creditor.PlayerId.Value,
                            step.Amount.Amount,
                            creditor.Money.Amount,
                            paymentReason,
                            liquidation.Obligation.DebtorPlayerId.Value,
                            liquidation.Obligation.TileId?.Value,
                            liquidation.Obligation.CardId?.Value));
                    }

                    break;
            }
        }

        return deltas.Count == 0 ? null : deltas;
    }

    private static IReadOnlyList<MoneyDeltaPayload>? CreateLiquidationMoneyDeltas(
        MultiCreditorLiquidationExecutionResult liquidation,
        string paymentReason,
        GameState gameState)
    {
        if (!liquidation.PaymentExecuted)
        {
            return null;
        }

        var deltas = new List<MoneyDeltaPayload>();
        foreach (var step in liquidation.Steps)
        {
            switch (step.StepKind)
            {
                case LiquidationStepKind.UpgradeSale:
                    deltas.Add(CreateDebtorLiquidationDelta(liquidation, step, "liquidation_upgrade_sale"));
                    break;
                case LiquidationStepKind.Mortgage:
                    deltas.Add(CreateDebtorLiquidationDelta(liquidation, step, "liquidation_mortgage"));
                    break;
                case LiquidationStepKind.PlayerPayment:
                    deltas.Add(CreateDebtorLiquidationDelta(liquidation, step, paymentReason));
                    if (step.CreditorPlayerId is not null)
                    {
                        var creditor = gameState.Players.First(player => player.PlayerId == step.CreditorPlayerId.Value);
                        deltas.Add(new MoneyDeltaPayload(
                            creditor.PlayerId.Value,
                            step.Amount.Amount,
                            creditor.Money.Amount,
                            paymentReason,
                            liquidation.Obligation.DebtorPlayerId.Value,
                            liquidation.Obligation.TileId?.Value,
                            liquidation.Obligation.CardId?.Value));
                    }

                    break;
            }
        }

        return deltas.Count == 0 ? null : deltas;
    }

    private static MoneyDeltaPayload CreateDebtorLiquidationDelta(
        LiquidationExecutionResult liquidation,
        LiquidationStepResult step,
        string reason)
    {
        var delta = step.StepKind is LiquidationStepKind.BankPayment or LiquidationStepKind.PlayerPayment
            ? -step.Amount.Amount
            : step.Amount.Amount;
        var counterpartyPlayerId = step.StepKind == LiquidationStepKind.PlayerPayment &&
            liquidation.Obligation.Creditor.PlayerId is not null
                ? liquidation.Obligation.Creditor.PlayerId.Value.Value
                : null;

        return new MoneyDeltaPayload(
            liquidation.Obligation.DebtorPlayerId.Value,
            delta,
            step.DebtorBalance.Amount,
            reason,
            counterpartyPlayerId,
            step.PropertyTileId?.Value ?? liquidation.Obligation.TileId?.Value,
            liquidation.Obligation.CardId?.Value);
    }

    private static MoneyDeltaPayload CreateDebtorLiquidationDelta(
        MultiCreditorLiquidationExecutionResult liquidation,
        LiquidationStepResult step,
        string reason)
    {
        var delta = step.StepKind == LiquidationStepKind.PlayerPayment
            ? -step.Amount.Amount
            : step.Amount.Amount;

        return new MoneyDeltaPayload(
            liquidation.Obligation.DebtorPlayerId.Value,
            delta,
            step.DebtorBalance.Amount,
            reason,
            step.CreditorPlayerId?.Value,
            step.PropertyTileId?.Value ?? liquidation.Obligation.TileId?.Value,
            liquidation.Obligation.CardId?.Value);
    }

    private static IReadOnlyList<LiquidationStepPayload>? CreateLiquidationStepPayloads(
        LiquidationExecutionResult? liquidation)
    {
        if (liquidation?.PaymentExecuted != true)
        {
            return null;
        }

        if (liquidation.Steps.Count == 0)
        {
            return null;
        }

        return liquidation.Steps
            .Select(step => new LiquidationStepPayload(
                FormatLiquidationStepKind(step.StepKind),
                step.PropertyTileId?.Value,
                step.Amount.Amount,
                step.DebtorBalance.Amount,
                step.UpgradeLevel,
                step.IsMortgaged))
            .ToArray();
    }

    private static IReadOnlyList<LiquidationStepPayload>? CreateLiquidationStepPayloads(
        MultiCreditorLiquidationExecutionResult? liquidation)
    {
        if (liquidation?.PaymentExecuted != true)
        {
            return null;
        }

        if (liquidation.Steps.Count == 0)
        {
            return null;
        }

        return liquidation.Steps
            .Select(step => new LiquidationStepPayload(
                FormatLiquidationStepKind(step.StepKind),
                step.PropertyTileId?.Value,
                step.Amount.Amount,
                step.DebtorBalance.Amount,
                step.UpgradeLevel,
                step.IsMortgaged))
            .ToArray();
    }

    private static IReadOnlyList<MoneyDeltaPayload>? CreateEndTurnMoneyDeltas(
        GameState previousGameState,
        GameState gameState)
    {
        if (gameState.CurrentTurnPlayerId is null)
        {
            return CreateMoneyDeltasFromDiff(previousGameState, gameState, "loan_interest");
        }

        var previousPlayer = previousGameState.Players.FirstOrDefault(
            player => player.PlayerId == gameState.CurrentTurnPlayerId.Value);
        var player = gameState.Players.FirstOrDefault(
            candidate => candidate.PlayerId == gameState.CurrentTurnPlayerId.Value);
        if (previousPlayer is null || player is null)
        {
            return CreateMoneyDeltasFromDiff(previousGameState, gameState, "loan_interest");
        }

        var deltas = new List<MoneyDeltaPayload>();
        var loanInterestDelta = CalculateStartTurnLoanInterestDelta(
            previousPlayer,
            LoanSharkConfig.FromRules(previousGameState.Rules.Loans));
        if (loanInterestDelta != 0)
        {
            deltas.Add(new MoneyDeltaPayload(
                player.PlayerId.Value,
                loanInterestDelta,
                previousPlayer.Money.Amount + loanInterestDelta,
                "loan_interest"));
        }

        var totalDelta = player.Money.Amount - previousPlayer.Money.Amount;
        var propertyRepairDelta = totalDelta - loanInterestDelta;
        if (propertyRepairDelta != 0)
        {
            deltas.Add(new MoneyDeltaPayload(
                player.PlayerId.Value,
                propertyRepairDelta,
                player.Money.Amount,
                "property_repair"));
        }

        return deltas.Count == 0 ? null : deltas;
    }

    private static int CalculateStartTurnLoanInterestDelta(Player previousPlayer, LoanSharkConfig config)
    {
        if (!config.Enabled)
        {
            return 0;
        }

        var loanState = previousPlayer.LoanState;
        if (loanState is null)
        {
            return 0;
        }

        var calculatedInterest = loanState.TotalBorrowed.Amount * loanState.CurrentInterestRatePercent / 100;
        return -Math.Max(calculatedInterest, config.MinimumInterestPayment);
    }

    private static IReadOnlyList<PlayerEliminationPayload>? CreatePlayerEliminationsFromDiff(
        GameState previousGameState,
        GameState gameState,
        string reason,
        int? paymentDue = null)
    {
        var eliminations = gameState.Players
            .Where(player => player.IsEliminated)
            .Where(player => previousGameState.Players.FirstOrDefault(
                previousPlayer => previousPlayer.PlayerId == player.PlayerId)?.IsEliminated == false)
            .Select(player => new PlayerEliminationPayload(
                player.PlayerId.Value,
                reason,
                player.Money.Amount,
                paymentDue))
            .ToArray();

        return eliminations.Length == 0 ? null : eliminations;
    }

    private static IReadOnlyList<PlayerEliminationPayload>? CreatePlayerEliminationPayloads(
        PlayerEliminationResult? elimination)
    {
        if (elimination?.WasEliminated != true)
        {
            return null;
        }

        return new[]
        {
            new PlayerEliminationPayload(
                elimination.PlayerId.Value,
                FormatEliminationReason(elimination.Reason),
                elimination.Balance.Amount,
                elimination.PaymentDue?.Amount),
        };
    }

    private static int? GetCardPaymentDue(
        PaymentObligation? singleDebtorObligation,
        MultiCreditorLiquidationExecutionResult? multiCreditorLiquidation)
    {
        if (singleDebtorObligation is not null)
        {
            return singleDebtorObligation.Amount.Amount;
        }

        if (multiCreditorLiquidation is not null)
        {
            return multiCreditorLiquidation.AmountDue.Amount;
        }

        return null;
    }

    private static IReadOnlyList<MoneyDeltaPayload>? CreateAuctionMoneyDeltas(
        AuctionFinalizationResult finalizationResult,
        GameState gameState)
    {
        if (finalizationResult.ResultKind != AuctionFinalizationResultKind.FinalizedWithWinner ||
            finalizationResult.WinnerId is null ||
            finalizationResult.WinningBid is null)
        {
            return null;
        }

        if (finalizationResult.PaymentLiquidation?.PaymentExecuted == true)
        {
            return CreateLiquidationMoneyDeltas(
                finalizationResult.PaymentLiquidation,
                "auction_payment",
                gameState);
        }

        var winner = gameState.Players.First(player => player.PlayerId == finalizationResult.WinnerId.Value);
        return new[]
        {
            new MoneyDeltaPayload(
                winner.PlayerId.Value,
                -finalizationResult.WinningBid.Value.Amount,
                winner.Money.Amount,
                "auction_payment",
                CounterpartyPlayerId: null,
                TileId: finalizationResult.PropertyTileId.Value),
        };
    }

    private static IReadOnlyList<PropertyOwnershipChangePayload>? CreateAuctionOwnershipChanges(
        AuctionFinalizationResult finalizationResult)
    {
        if (finalizationResult.ResultKind != AuctionFinalizationResultKind.FinalizedWithWinner ||
            finalizationResult.WinnerId is null)
        {
            return null;
        }

        return new[]
        {
            new PropertyOwnershipChangePayload(
                finalizationResult.PropertyTileId.Value,
                PreviousOwnerPlayerId: null,
                NewOwnerPlayerId: finalizationResult.WinnerId.Value.Value,
                Reason: "auction_won"),
        };
    }

    private static IReadOnlyList<PropertyOwnershipChangePayload>? CreateTradeOwnershipChanges(
        TradeSettlementResult settlement)
    {
        if (settlement.OwnershipChanges.Count == 0)
        {
            return null;
        }

        return settlement.OwnershipChanges
            .Select(change => new PropertyOwnershipChangePayload(
                change.PropertyTileId.Value,
                change.PreviousOwnerId.Value,
                change.NewOwnerId.Value,
                "trade"))
            .ToArray();
    }

    private static IReadOnlyList<PlayerEliminationPayload>? CreateAuctionPlayerEliminations(
        AuctionFinalizationResult finalizationResult,
        GameState gameState)
    {
        if (finalizationResult.EliminationResult is null ||
            !finalizationResult.EliminationResult.WasEliminated ||
            finalizationResult.WinnerId is null)
        {
            return null;
        }

        var player = gameState.Players.First(updatedPlayer => updatedPlayer.PlayerId == finalizationResult.WinnerId.Value);
        return new[]
        {
            new PlayerEliminationPayload(
                player.PlayerId.Value,
                FormatEliminationReason(finalizationResult.EliminationResult.Reason),
                player.Money.Amount,
                finalizationResult.WinningBid?.Amount),
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
            auctionState.CountdownDurationSeconds,
            auctionState.TimerEndsAtUtc);
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
            CardResolutionActionKind.ApplySlimer or
            CardResolutionActionKind.ApplyEarthquake or
            CardResolutionActionKind.GoToLockup or
            CardResolutionActionKind.GetOutOfLockup;
    }

    private static bool ShouldDiscardCard(CardResolutionActionKind actionKind)
    {
        return actionKind != CardResolutionActionKind.GetOutOfLockup;
    }

    private static string GetCardExecutionKind(CardResolutionResult cardResolution, Player player)
    {
        if (cardResolution.ActionKind == CardResolutionActionKind.GetOutOfLockup &&
            player.HeldCardIds.Contains(cardResolution.CardId))
        {
            return "card_held";
        }

        if (cardResolution.ActionKind == CardResolutionActionKind.PayMoney && player.IsEliminated)
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

    private static PendingTradeOffer? FindPendingTradeOffer(GameSession session, string tradeOfferId)
    {
        return session.PendingTradeOffers.FirstOrDefault(
            offer => string.Equals(offer.TradeOfferId, tradeOfferId, StringComparison.Ordinal));
    }

    private static int GetPropertyDamagePercent(GameState gameState, TileId tileId)
    {
        return gameState.PropertyStates.TryGetValue(tileId, out var propertyState)
            ? propertyState.Data.DamagePercent
            : 0;
    }

    private static Money? CalculateRepairCost(GameState gameState, TileId tileId, int repairedDamagePercent)
    {
        if (repairedDamagePercent <= 0)
        {
            return null;
        }

        var tile = gameState.Board.Tiles.FirstOrDefault(candidate => candidate.TileId == tileId);
        if (tile?.Price is null)
        {
            return null;
        }

        return new Money(Math.Max(
            1,
            (int)Math.Floor(tile.Price.Value.Amount * repairedDamagePercent / 100m)));
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
            CardResolutionActionKind.ApplySlimer => "apply_slimer",
            CardResolutionActionKind.ApplyEarthquake => "apply_earthquake",
            CardResolutionActionKind.GoToLockup => "go_to_lockup",
            CardResolutionActionKind.GetOutOfLockup => "get_out_of_lockup",
            _ => actionKind.ToString().ToLowerInvariant(),
        };
    }

    private static string FormatLiquidationStepKind(LiquidationStepKind stepKind)
    {
        return stepKind switch
        {
            LiquidationStepKind.UpgradeSale => "upgrade_sale",
            LiquidationStepKind.Mortgage => "mortgage",
            LiquidationStepKind.BankPayment => "bank_payment",
            LiquidationStepKind.PlayerPayment => "player_payment",
            _ => stepKind.ToString().ToLowerInvariant(),
        };
    }

    private static string FormatPlayerStatusEffectKind(PlayerStatusEffectKind kind)
    {
        return kind switch
        {
            PlayerStatusEffectKind.NoOp => "no_op",
            PlayerStatusEffectKind.Slimer => "slimer",
            _ => kind.ToString().ToLowerInvariant(),
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
