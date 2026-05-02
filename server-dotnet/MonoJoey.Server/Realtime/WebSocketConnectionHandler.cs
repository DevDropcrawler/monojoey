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
        : this(connectionManager, lobbyMessageHandler, lobbyMessageHandler.AuctionTimerService)
    {
    }

    public WebSocketConnectionHandler(
        IWebSocketConnectionManager connectionManager,
        LobbyMessageHandler lobbyMessageHandler,
        AuctionTimerService auctionTimerService)
    {
        this.connectionManager = connectionManager;
        this.lobbyMessageHandler = lobbyMessageHandler;
        auctionTimerService.SetExpiryHandler(async (sessionId, timerEndsAtUtc, cancellationToken) =>
        {
            var result = lobbyMessageHandler.HandleAuctionTimerExpired(sessionId, timerEndsAtUtc);
            if (result is not null)
            {
                await BroadcastIfNeededAsync(result, cancellationToken);
            }
        });
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
                        connection,
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

                var handleResult = lobbyMessageHandler.HandleTextMessageResult(message, lobbyConnectionContext);
                var directResponse = SerializeDirectResponse(handleResult.DirectResponse);
                await SendTextAsync(connection, directResponse, cancellationToken);
                await BroadcastIfNeededAsync(handleResult, cancellationToken);
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
        WebSocketConnection connection,
        string message,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(message);

        await connection.SendGate.WaitAsync(cancellationToken);
        try
        {
            await connection.WebSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }
        finally
        {
            connection.SendGate.Release();
        }
    }

    private static string SerializeDirectResponse(LobbyServerEnvelope response)
    {
        return System.Text.Json.JsonSerializer.Serialize(
            response,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    }

    private async Task BroadcastIfNeededAsync(
        LobbyMessageHandleResult result,
        CancellationToken cancellationToken)
    {
        if (result.Broadcasts.Count == 0 || result.BroadcastConnectionIds.Count == 0)
        {
            return;
        }

        foreach (var broadcast in result.Broadcasts)
        {
            var message = lobbyMessageHandler.SerializeBroadcastMessage(broadcast);
            foreach (var connectionId in result.BroadcastConnectionIds)
            {
                var connection = connectionManager.Get(connectionId);
                if (connection is null || connection.WebSocket.State != WebSocketState.Open)
                {
                    continue;
                }

                try
                {
                    await SendTextAsync(connection, message, cancellationToken);
                }
                catch (Exception exception) when (
                    exception is WebSocketException or ObjectDisposedException or InvalidOperationException)
                {
                }
            }
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
