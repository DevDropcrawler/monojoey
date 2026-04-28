namespace MonoJoey.Server.Sessions;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public sealed class SessionManager
{
    private const int MinimumPlayersToStart = 2;
    private const int StartingMoney = 1500;

    private readonly Dictionary<string, GameSession> sessions = new();

    public GameSession CreateSession()
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var gameState = new GameState(
            new MatchId(sessionId),
            GamePhase.Lobby,
            DefaultBoardFactory.Create(),
            Array.Empty<Player>(),
            CurrentTurnPlayerId: null,
            TurnNumber: 0,
            DateTimeOffset.UtcNow,
            EndedAtUtc: null);

        var session = new GameSession(
            sessionId,
            Array.Empty<PlayerConnection>(),
            gameState,
            GameSessionStatus.Lobby);

        sessions.Add(sessionId, session);

        return session;
    }

    public GameSession JoinSession(string sessionId, PlayerConnection player)
    {
        var session = FindSession(sessionId);

        if (session.Status != GameSessionStatus.Lobby)
        {
            throw new InvalidOperationException("Session is not in lobby status.");
        }

        if (session.Players.Any(existingPlayer => existingPlayer.PlayerId == player.PlayerId))
        {
            return session;
        }

        var updatedSession = session with
        {
            Players = session.Players.Append(player).ToArray(),
        };

        sessions[sessionId] = updatedSession;

        return updatedSession;
    }

    public GameSession LeaveSession(string sessionId, PlayerConnection player)
    {
        var session = FindSession(sessionId);
        var updatedPlayers = session.Players
            .Where(existingPlayer => existingPlayer.PlayerId != player.PlayerId)
            .ToArray();

        var updatedSession = session with
        {
            Players = updatedPlayers,
        };

        sessions[sessionId] = updatedSession;

        return updatedSession;
    }

    public GameSession SetReady(string sessionId, PlayerId playerId, bool isReady)
    {
        var session = FindSession(sessionId);

        if (session.Status != GameSessionStatus.Lobby)
        {
            throw new InvalidOperationException("Session is not in lobby status.");
        }

        var playerExists = false;
        var updatedPlayers = session.Players
            .Select(player =>
            {
                if (player.PlayerId != playerId)
                {
                    return player;
                }

                playerExists = true;
                return player with { IsReady = isReady };
            })
            .ToArray();

        if (!playerExists)
        {
            throw new InvalidOperationException("Player is not in lobby.");
        }

        var updatedSession = session with
        {
            Players = updatedPlayers,
        };

        sessions[sessionId] = updatedSession;

        return updatedSession;
    }

    public GameSession StartGame(string sessionId)
    {
        var session = FindSession(sessionId);

        if (session.Status != GameSessionStatus.Lobby)
        {
            throw new InvalidOperationException("Session is not in lobby status.");
        }

        if (session.Players.Count < MinimumPlayersToStart)
        {
            throw new InvalidOperationException("Not enough players to start the game.");
        }

        if (session.Players.Any(player => !player.IsReady))
        {
            throw new InvalidOperationException("All players must be ready to start the game.");
        }

        var startTileId = GetStartTileId(session.GameState.Board);
        var enginePlayers = session.Players
            .Select(player => CreateEnginePlayer(player.PlayerId, startTileId))
            .ToArray();

        var lobbyGameState = new GameState(
            session.GameState.MatchId,
            GamePhase.Lobby,
            session.GameState.Board,
            enginePlayers,
            CurrentTurnPlayerId: null,
            TurnNumber: 0,
            DateTimeOffset.UtcNow,
            EndedAtUtc: null);
        var startedGameState = TurnManager.StartFirstTurn(lobbyGameState);

        var updatedSession = session with
        {
            GameState = startedGameState,
            Status = GameSessionStatus.InGame,
        };

        sessions[sessionId] = updatedSession;

        return updatedSession;
    }

    public GameSession? GetSession(string sessionId)
    {
        return sessions.TryGetValue(sessionId, out var session)
            ? session
            : null;
    }

    public GameSession UpdateGameState(string sessionId, GameState gameState)
    {
        var session = FindSession(sessionId);
        var updatedSession = session with
        {
            GameState = gameState,
        };

        sessions[sessionId] = updatedSession;

        return updatedSession;
    }

    private GameSession FindSession(string sessionId)
    {
        if (sessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        throw new InvalidOperationException("Session not found.");
    }

    private static TileId GetStartTileId(Board board)
    {
        var startTile = board.Tiles.FirstOrDefault(tile => tile.TileType == TileType.Start)
            ?? board.Tiles.OrderBy(tile => tile.Index).FirstOrDefault();

        if (startTile is null)
        {
            throw new InvalidOperationException("Board must contain at least one tile.");
        }

        return startTile.TileId;
    }

    private static Player CreateEnginePlayer(PlayerId playerId, TileId startTileId)
    {
        return new Player(
            playerId,
            playerId.Value,
            $"token_{playerId.Value}",
            $"color_{playerId.Value}",
            new Money(StartingMoney),
            startTileId,
            new HashSet<TileId>(),
            new HashSet<CardId>(),
            IsBankrupt: false,
            IsEliminated: false);
    }
}
