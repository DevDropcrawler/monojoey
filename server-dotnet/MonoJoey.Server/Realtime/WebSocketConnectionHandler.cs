namespace MonoJoey.Server.Realtime;

using System.Net.WebSockets;

public sealed class WebSocketConnectionHandler
{
    private readonly IWebSocketConnectionManager connectionManager;

    public WebSocketConnectionHandler(IWebSocketConnectionManager connectionManager)
    {
        this.connectionManager = connectionManager;
    }

    public async Task HandleAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webSocket);

        var connection = connectionManager.Add(webSocket);
        var buffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException)
        {
        }
        finally
        {
            _ = connectionManager.Remove(connection.ConnectionId);
            await CloseIfPossible(webSocket);
        }
    }

    private static async Task CloseIfPossible(WebSocket webSocket)
    {
        if (webSocket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            await webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Connection closed.",
                CancellationToken.None);
        }
        catch (Exception exception) when (
            exception is WebSocketException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }
}
