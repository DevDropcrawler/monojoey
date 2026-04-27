namespace MonoJoey.Server.Realtime;

public sealed class LobbyConnectionContext
{
    public LobbyConnectionContext(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        ConnectionId = connectionId;
    }

    public string ConnectionId { get; }

    public string? SessionId { get; private set; }

    public string? PlayerId { get; private set; }

    public bool IsBound => SessionId is not null && PlayerId is not null;

    public bool IsBoundToDifferentPlayer(string playerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);

        return PlayerId is not null && !string.Equals(PlayerId, playerId, StringComparison.Ordinal);
    }

    public void Bind(string sessionId, string playerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);

        SessionId = sessionId;
        PlayerId = playerId;
    }

    public void ClearBinding()
    {
        SessionId = null;
        PlayerId = null;
    }
}
