namespace MonoJoey.Server.Realtime;

using System.Net.WebSockets;
using System.Text;

public sealed class WebSocketConnectionHandler
{
    private readonly IWebSocketConnectionManager connectionManager;
    private readonly LobbyMessageHandler lobbyMessageHandler;

    public WebSocketConnectionHandler(
        IWebSocketConnectionManager connectionManager,
        LobbyMessageHandler lobbyMessageHandler)
    {
        this.connectionManager = connectionManager;
        this.lobbyMessageHandler = lobbyMessageHandler;
    }

    public async Task HandleAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webSocket);

        var connection = connectionManager.Add(webSocket);
        var lobbyConnectionContext = new LobbyConnectionContext(connection.ConnectionId);
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

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    await DrainMessageIfNeeded(webSocket, buffer, result, cancellationToken);
                    await SendTextAsync(
                        webSocket,
                        lobbyMessageHandler.CreateErrorMessage(
                            LobbyErrorCodes.InvalidMessage,
                            "Binary messages are not supported."),
                        cancellationToken);
                    continue;
                }

                var message = await ReadTextMessageAsync(webSocket, buffer, result, cancellationToken);
                if (message is null)
                {
                    break;
                }

                var response = lobbyMessageHandler.HandleTextMessage(message, lobbyConnectionContext);
                await SendTextAsync(webSocket, response, cancellationToken);
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
            lobbyMessageHandler.CleanupConnection(lobbyConnectionContext);
            _ = connectionManager.Remove(connection.ConnectionId);
            await CloseIfPossible(webSocket);
        }
    }

    private static async Task<string?> ReadTextMessageAsync(
        WebSocket webSocket,
        byte[] buffer,
        WebSocketReceiveResult firstResult,
        CancellationToken cancellationToken)
    {
        using var messageBuffer = new MemoryStream();
        var result = firstResult;

        while (true)
        {
            messageBuffer.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(messageBuffer.ToArray());
            }

            result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }
        }
    }

    private static async Task DrainMessageIfNeeded(
        WebSocket webSocket,
        byte[] buffer,
        WebSocketReceiveResult firstResult,
        CancellationToken cancellationToken)
    {
        var result = firstResult;

        while (!result.EndOfMessage)
        {
            result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }
        }
    }

    private static async Task SendTextAsync(
        WebSocket webSocket,
        string message,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(message);

        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
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
