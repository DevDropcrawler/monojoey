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
            LobbyMessageTypes.LobbyState or
                LobbyMessageTypes.GameStarted or
                LobbyMessageTypes.RollResult or
                LobbyMessageTypes.ResolveTileResult or
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
