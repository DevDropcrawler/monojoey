namespace MonoJoey.Server.Realtime;

using System.Net.WebSockets;

public interface IWebSocketConnectionManager
{
    int Count { get; }

    WebSocketConnection Add(WebSocket webSocket);

    WebSocketConnection? Get(string connectionId);

    bool Remove(string connectionId);

    IReadOnlyCollection<WebSocketConnection> Snapshot();
}
