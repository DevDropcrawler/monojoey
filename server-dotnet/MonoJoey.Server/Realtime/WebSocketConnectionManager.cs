namespace MonoJoey.Server.Realtime;

using System.Collections.Concurrent;
using System.Net.WebSockets;

public sealed class WebSocketConnectionManager : IWebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocketConnection> connections = new();

    public int Count => connections.Count;

    public WebSocketConnection Add(WebSocket webSocket)
    {
        ArgumentNullException.ThrowIfNull(webSocket);

        var connection = new WebSocketConnection(
            Guid.NewGuid().ToString("N"),
            webSocket,
            DateTimeOffset.UtcNow);

        if (!connections.TryAdd(connection.ConnectionId, connection))
        {
            throw new InvalidOperationException("Unable to register WebSocket connection.");
        }

        return connection;
    }

    public WebSocketConnection? Get(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        return connections.GetValueOrDefault(connectionId);
    }

    public bool Remove(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        return connections.TryRemove(connectionId, out _);
    }

    public IReadOnlyCollection<WebSocketConnection> Snapshot()
    {
        return connections.Values.ToArray();
    }
}
