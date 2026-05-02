namespace MonoJoey.Server.Realtime;

using System.Net.WebSockets;

public sealed record WebSocketConnection(
    string ConnectionId,
    WebSocket WebSocket,
    DateTimeOffset ConnectedAtUtc)
{
    public SemaphoreSlim SendGate { get; } = new(1, 1);
}
