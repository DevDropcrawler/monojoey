namespace MonoJoey.Server.Tests.Realtime;

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MonoJoey.Server.Realtime;
using MonoJoey.Server.Sessions;

public class WebSocketConnectionHandlerTests
{
    [Fact]
    public async Task Disconnect_RemovesBoundPlayerFromSession()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var connectionManager = new WebSocketConnectionManager();
        var lobbyMessageHandler = new LobbyMessageHandler(sessionManager);
        var handler = new WebSocketConnectionHandler(connectionManager, lobbyMessageHandler);
        using var webSocket = new ScriptedWebSocket(
            TextFrame(JoinMessage(session.SessionId, "player_1")),
            CloseFrame());

        await handler.HandleAsync(webSocket, CancellationToken.None);

        Assert.Empty(sessionManager.GetSession(session.SessionId)?.Players ?? Array.Empty<PlayerConnection>());
        Assert.Equal(0, connectionManager.Count);
        var sent = Assert.Single(webSocket.SentTextMessages);
        using var response = JsonDocument.Parse(sent);
        Assert.Equal("lobby_state", response.RootElement.GetProperty("type").GetString());
    }

    private static ReceivedFrame TextFrame(string message)
    {
        return new ReceivedFrame(
            WebSocketMessageType.Text,
            Encoding.UTF8.GetBytes(message),
            EndOfMessage: true);
    }

    private static ReceivedFrame CloseFrame()
    {
        return new ReceivedFrame(
            WebSocketMessageType.Close,
            Array.Empty<byte>(),
            EndOfMessage: true);
    }

    private static string JoinMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""join_lobby"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private sealed record ReceivedFrame(
        WebSocketMessageType MessageType,
        byte[] Payload,
        bool EndOfMessage);

    private sealed class ScriptedWebSocket : WebSocket
    {
        private readonly Queue<ReceivedFrame> frames;
        private WebSocketState state = WebSocketState.Open;

        public ScriptedWebSocket(params ReceivedFrame[] frames)
        {
            this.frames = new Queue<ReceivedFrame>(frames);
        }

        public IReadOnlyList<string> SentTextMessages => sentTextMessages;

        private List<string> sentTextMessages { get; } = new();

        public override WebSocketCloseStatus? CloseStatus => null;

        public override string? CloseStatusDescription => null;

        public override WebSocketState State => state;

        public override string? SubProtocol => null;

        public override void Abort()
        {
            state = WebSocketState.Aborted;
        }

        public override Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            state = WebSocketState.Closed;
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            if (frames.Count == 0)
            {
                state = WebSocketState.CloseReceived;
                return Task.FromResult(new WebSocketReceiveResult(
                    count: 0,
                    messageType: WebSocketMessageType.Close,
                    endOfMessage: true));
            }

            var frame = frames.Dequeue();
            Array.Copy(frame.Payload, 0, buffer.Array!, buffer.Offset, frame.Payload.Length);

            if (frame.MessageType == WebSocketMessageType.Close)
            {
                state = WebSocketState.CloseReceived;
            }

            return Task.FromResult(new WebSocketReceiveResult(
                frame.Payload.Length,
                frame.MessageType,
                frame.EndOfMessage));
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            if (messageType == WebSocketMessageType.Text)
            {
                sentTextMessages.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
            }

            return Task.CompletedTask;
        }
    }
}
