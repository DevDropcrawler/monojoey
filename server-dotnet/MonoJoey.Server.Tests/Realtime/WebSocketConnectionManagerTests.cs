namespace MonoJoey.Server.Tests.Realtime;

using System.Net.WebSockets;
using MonoJoey.Server.Realtime;

public class WebSocketConnectionManagerTests
{
    [Fact]
    public void Add_StoresConnectionByGeneratedId()
    {
        var manager = new WebSocketConnectionManager();
        using var webSocket = new TestWebSocket();

        var connection = manager.Add(webSocket);

        Assert.False(string.IsNullOrWhiteSpace(connection.ConnectionId));
        Assert.Same(webSocket, connection.WebSocket);
        Assert.Same(connection, manager.Get(connection.ConnectionId));
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void Remove_DeletesConnectionAndMissingIdIsSafe()
    {
        var manager = new WebSocketConnectionManager();
        using var webSocket = new TestWebSocket();
        var connection = manager.Add(webSocket);

        var removedExisting = manager.Remove(connection.ConnectionId);
        var removedMissing = manager.Remove(connection.ConnectionId);

        Assert.True(removedExisting);
        Assert.False(removedMissing);
        Assert.Null(manager.Get(connection.ConnectionId));
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void Add_MultipleConnectionsProduceDistinctIds()
    {
        var manager = new WebSocketConnectionManager();
        using var firstWebSocket = new TestWebSocket();
        using var secondWebSocket = new TestWebSocket();

        var firstConnection = manager.Add(firstWebSocket);
        var secondConnection = manager.Add(secondWebSocket);

        Assert.NotEqual(firstConnection.ConnectionId, secondConnection.ConnectionId);
    }

    [Fact]
    public void CountAndSnapshot_ReflectActiveConnections()
    {
        var manager = new WebSocketConnectionManager();
        using var firstWebSocket = new TestWebSocket();
        using var secondWebSocket = new TestWebSocket();
        var firstConnection = manager.Add(firstWebSocket);
        var secondConnection = manager.Add(secondWebSocket);

        var snapshot = manager.Snapshot();

        Assert.Equal(2, manager.Count);
        Assert.Equal(2, snapshot.Count);
        Assert.Contains(firstConnection, snapshot);
        Assert.Contains(secondConnection, snapshot);

        _ = manager.Remove(firstConnection.ConnectionId);

        var updatedSnapshot = manager.Snapshot();
        Assert.Equal(1, manager.Count);
        Assert.Single(updatedSnapshot);
        Assert.Contains(secondConnection, updatedSnapshot);
    }

    private sealed class TestWebSocket : WebSocket
    {
        public override WebSocketCloseStatus? CloseStatus => null;

        public override string? CloseStatusDescription => null;

        public override WebSocketState State { get; } = WebSocketState.Open;

        public override string? SubProtocol => null;

        public override void Abort()
        {
        }

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
