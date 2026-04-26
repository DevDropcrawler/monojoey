namespace MonoJoey.Server.Sessions;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public sealed class SessionManager
{
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

    public GameSession? GetSession(string sessionId)
    {
        return sessions.TryGetValue(sessionId, out var session)
            ? session
            : null;
    }

    private GameSession FindSession(string sessionId)
    {
        if (sessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        throw new InvalidOperationException("Session not found.");
    }
}
