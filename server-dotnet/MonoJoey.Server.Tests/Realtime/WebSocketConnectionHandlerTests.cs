namespace MonoJoey.Server.Tests.Realtime;

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MonoJoey.Server.GameEngine;
using MonoJoey.Server.Realtime;
using MonoJoey.Server.Sessions;
using MonoJoey.Shared.Protocol;

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

    [Fact]
    public async Task RollDice_SendsOneResponseForRollMessage()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_1"), "existing_connection_1", IsReady: false));
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_2"), "existing_connection_2", IsReady: true));
        var connectionManager = new WebSocketConnectionManager();
        var lobbyMessageHandler = new LobbyMessageHandler(
            sessionManager,
            new DiceService(new FixedDiceRoller(new DiceRoll(3, 3))));
        var handler = new WebSocketConnectionHandler(connectionManager, lobbyMessageHandler);
        using var webSocket = new ScriptedWebSocket(
            TextFrame(JoinMessage(session.SessionId, "player_1")),
            TextFrame(SetReadyMessage(session.SessionId, "player_1", isReady: true)),
            TextFrame(StartGameMessage(session.SessionId, "player_1")),
            TextFrame(RollDiceMessage(session.SessionId, "player_1")),
            TextFrame(ResolveTileMessage(session.SessionId, "player_1")),
            TextFrame(ExecuteTileMessage(session.SessionId, "player_1")),
            TextFrame(EndTurnMessage(session.SessionId, "player_1")),
            CloseFrame());

        await handler.HandleAsync(webSocket, CancellationToken.None);

        Assert.Equal(11, webSocket.SentTextMessages.Count);
        using var rollResponse = JsonDocument.Parse(webSocket.SentTextMessages[3]);
        Assert.Equal("roll_result", rollResponse.RootElement.GetProperty("type").GetString());
        using var rollBroadcast = JsonDocument.Parse(webSocket.SentTextMessages[4]);
        Assert.Equal("dice_rolled", rollBroadcast.RootElement.GetProperty("type").GetString());
        using var resolveResponse = JsonDocument.Parse(webSocket.SentTextMessages[5]);
        Assert.Equal("resolve_tile_result", resolveResponse.RootElement.GetProperty("type").GetString());
        using var resolveBroadcast = JsonDocument.Parse(webSocket.SentTextMessages[6]);
        Assert.Equal("tile_resolved", resolveBroadcast.RootElement.GetProperty("type").GetString());
        using var executeResponse = JsonDocument.Parse(webSocket.SentTextMessages[7]);
        Assert.Equal("execute_tile_result", executeResponse.RootElement.GetProperty("type").GetString());
        using var executeBroadcast = JsonDocument.Parse(webSocket.SentTextMessages[8]);
        Assert.Equal("tile_executed", executeBroadcast.RootElement.GetProperty("type").GetString());
        using var endTurnResponse = JsonDocument.Parse(webSocket.SentTextMessages[9]);
        Assert.Equal("end_turn_result", endTurnResponse.RootElement.GetProperty("type").GetString());
        using var endTurnBroadcast = JsonDocument.Parse(webSocket.SentTextMessages[10]);
        Assert.Equal("turn_ended", endTurnBroadcast.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task CardTileExecute_SendsOneExecuteTileResultResponse()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_1"), "existing_connection_1", IsReady: false));
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_2"), "existing_connection_2", IsReady: true));
        var connectionManager = new WebSocketConnectionManager();
        var lobbyMessageHandler = new LobbyMessageHandler(
            sessionManager,
            new DiceService(new FixedDiceRoller(new DiceRoll(1, 1))));
        var handler = new WebSocketConnectionHandler(connectionManager, lobbyMessageHandler);
        using var webSocket = new ScriptedWebSocket(
            TextFrame(JoinMessage(session.SessionId, "player_1")),
            TextFrame(SetReadyMessage(session.SessionId, "player_1", isReady: true)),
            TextFrame(StartGameMessage(session.SessionId, "player_1")),
            TextFrame(RollDiceMessage(session.SessionId, "player_1")),
            TextFrame(ResolveTileMessage(session.SessionId, "player_1")),
            TextFrame(ExecuteTileMessage(session.SessionId, "player_1")),
            CloseFrame());

        await handler.HandleAsync(webSocket, CancellationToken.None);

        Assert.Equal(9, webSocket.SentTextMessages.Count);
        using var executeResponse = JsonDocument.Parse(webSocket.SentTextMessages[7]);
        var payload = executeResponse.RootElement.GetProperty("payload");
        Assert.Equal("execute_tile_result", executeResponse.RootElement.GetProperty("type").GetString());
        Assert.Equal("card_executed", payload.GetProperty("executionKind").GetString());
        Assert.Equal("chance", payload.GetProperty("card").GetProperty("deckId").GetString());
    }

    [Fact]
    public async Task PlaceBid_SendsOneResponseForBidMessage()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_1"), "existing_connection_1", IsReady: false));
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_2"), "existing_connection_2", IsReady: true));
        var connectionManager = new WebSocketConnectionManager();
        var lobbyMessageHandler = new LobbyMessageHandler(
            sessionManager,
            new DiceService(new FixedDiceRoller(new DiceRoll(1, 2))));
        var handler = new WebSocketConnectionHandler(connectionManager, lobbyMessageHandler);
        using var webSocket = new ScriptedWebSocket(
            TextFrame(JoinMessage(session.SessionId, "player_1")),
            TextFrame(SetReadyMessage(session.SessionId, "player_1", isReady: true)),
            TextFrame(StartGameMessage(session.SessionId, "player_1")),
            TextFrame(RollDiceMessage(session.SessionId, "player_1")),
            TextFrame(ResolveTileMessage(session.SessionId, "player_1")),
            TextFrame(ExecuteTileMessage(session.SessionId, "player_1")),
            TextFrame(PlaceBidMessage(session.SessionId, "player_1", 10)),
            CloseFrame());

        await handler.HandleAsync(webSocket, CancellationToken.None);

        Assert.Equal(11, webSocket.SentTextMessages.Count);
        using var bidResponse = JsonDocument.Parse(webSocket.SentTextMessages[9]);
        Assert.Equal("bid_result", bidResponse.RootElement.GetProperty("type").GetString());
        Assert.Equal(10, bidResponse.RootElement.GetProperty("payload").GetProperty("currentHighestBid").GetInt32());
        using var bidBroadcast = JsonDocument.Parse(webSocket.SentTextMessages[10]);
        Assert.Equal("bid_accepted", bidBroadcast.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task FinalizeAuction_SendsOneResponseForFinalizeMessage()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_1"), "existing_connection_1", IsReady: false));
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_2"), "existing_connection_2", IsReady: true));
        var connectionManager = new WebSocketConnectionManager();
        var lobbyMessageHandler = new LobbyMessageHandler(
            sessionManager,
            new DiceService(new FixedDiceRoller(new DiceRoll(1, 2))));
        var handler = new WebSocketConnectionHandler(connectionManager, lobbyMessageHandler);
        using var webSocket = new ScriptedWebSocket(
            TextFrame(JoinMessage(session.SessionId, "player_1")),
            TextFrame(SetReadyMessage(session.SessionId, "player_1", isReady: true)),
            TextFrame(StartGameMessage(session.SessionId, "player_1")),
            TextFrame(RollDiceMessage(session.SessionId, "player_1")),
            TextFrame(ResolveTileMessage(session.SessionId, "player_1")),
            TextFrame(ExecuteTileMessage(session.SessionId, "player_1")),
            TextFrame(PlaceBidMessage(session.SessionId, "player_1", 10)),
            TextFrame(FinalizeAuctionMessage(session.SessionId, "player_1")),
            CloseFrame());

        await handler.HandleAsync(webSocket, CancellationToken.None);

        Assert.Equal(13, webSocket.SentTextMessages.Count);
        using var auctionResponse = JsonDocument.Parse(webSocket.SentTextMessages[11]);
        Assert.Equal("auction_result", auctionResponse.RootElement.GetProperty("type").GetString());
        Assert.Equal("won", auctionResponse.RootElement.GetProperty("payload").GetProperty("resultType").GetString());
        using var auctionBroadcast = JsonDocument.Parse(webSocket.SentTextMessages[12]);
        Assert.Equal("auction_finalized", auctionBroadcast.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task TakeLoan_SendsOneResponseForLoanMessage()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_1"), "existing_connection_1", IsReady: false));
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_2"), "existing_connection_2", IsReady: true));
        var connectionManager = new WebSocketConnectionManager();
        var lobbyMessageHandler = new LobbyMessageHandler(
            sessionManager,
            new DiceService(new FixedDiceRoller(new DiceRoll(1, 2))));
        var handler = new WebSocketConnectionHandler(connectionManager, lobbyMessageHandler);
        using var webSocket = new ScriptedWebSocket(
            TextFrame(JoinMessage(session.SessionId, "player_1")),
            TextFrame(SetReadyMessage(session.SessionId, "player_1", isReady: true)),
            TextFrame(StartGameMessage(session.SessionId, "player_1")),
            TextFrame(TakeLoanMessage(session.SessionId, "player_1", 200, "rent_payment")),
            CloseFrame());

        await handler.HandleAsync(webSocket, CancellationToken.None);

        Assert.Equal(5, webSocket.SentTextMessages.Count);
        using var loanResponse = JsonDocument.Parse(webSocket.SentTextMessages[3]);
        var payload = loanResponse.RootElement.GetProperty("payload");
        Assert.Equal("loan_result", loanResponse.RootElement.GetProperty("type").GetString());
        Assert.Equal("player_1", payload.GetProperty("playerId").GetString());
        Assert.Equal(1700, payload.GetProperty("money").GetInt32());
        using var loanBroadcast = JsonDocument.Parse(webSocket.SentTextMessages[4]);
        Assert.Equal("loan_taken", loanBroadcast.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetSnapshot_SendsOneResponseForSnapshotMessage()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_1"), "existing_connection_1", IsReady: false));
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_2"), "existing_connection_2", IsReady: true));
        var connectionManager = new WebSocketConnectionManager();
        var lobbyMessageHandler = new LobbyMessageHandler(
            sessionManager,
            new DiceService(new FixedDiceRoller(new DiceRoll(1, 2))));
        var handler = new WebSocketConnectionHandler(connectionManager, lobbyMessageHandler);
        using var webSocket = new ScriptedWebSocket(
            TextFrame(JoinMessage(session.SessionId, "player_1")),
            TextFrame(SetReadyMessage(session.SessionId, "player_1", isReady: true)),
            TextFrame(StartGameMessage(session.SessionId, "player_1")),
            TextFrame(GetSnapshotMessage(session.SessionId, "player_1")),
            CloseFrame());

        await handler.HandleAsync(webSocket, CancellationToken.None);

        Assert.Equal(4, webSocket.SentTextMessages.Count);
        using var snapshotResponse = JsonDocument.Parse(webSocket.SentTextMessages[3]);
        Assert.Equal("snapshot_result", snapshotResponse.RootElement.GetProperty("type").GetString());
        Assert.Equal(1, snapshotResponse.RootElement.GetProperty("payload").GetProperty("snapshotVersion").GetInt32());
    }

    [Fact]
    public async Task RejectedGameplayActionSendsDirectErrorOnly()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var connectionManager = new WebSocketConnectionManager();
        var lobbyMessageHandler = new LobbyMessageHandler(
            sessionManager,
            new DiceService(new FixedDiceRoller(new DiceRoll(1, 2))));
        var handler = new WebSocketConnectionHandler(connectionManager, lobbyMessageHandler);
        using var webSocket = new ScriptedWebSocket(
            TextFrame(JoinMessage(session.SessionId, "player_1")),
            TextFrame(RollDiceMessage(session.SessionId, "player_1")),
            CloseFrame());

        await handler.HandleAsync(webSocket, CancellationToken.None);

        Assert.Equal(2, webSocket.SentTextMessages.Count);
        using var errorResponse = JsonDocument.Parse(webSocket.SentTextMessages[1]);
        Assert.Equal("error", errorResponse.RootElement.GetProperty("type").GetString());
        Assert.Equal(
            "invalid_session_state",
            errorResponse.RootElement.GetProperty("payload").GetProperty("code").GetString());
        Assert.Equal(0, sessionManager.GetSession(session.SessionId)?.LastEventSequence);
    }

    [Fact]
    public async Task RollDice_BroadcastsToSenderAndOtherInGameConnectionOnly()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_1"), "stale_sender_connection", IsReady: false));
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_2"), "other_connection", IsReady: true));
        var otherSession = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(
            otherSession.SessionId,
            new PlayerConnection(new PlayerId("player_3"), "other_session_connection", IsReady: true));
        var connectionManager = new TestConnectionManager("sender_connection");
        using var otherWebSocket = new ScriptedWebSocket();
        using var otherSessionWebSocket = new ScriptedWebSocket();
        connectionManager.Register("other_connection", otherWebSocket);
        connectionManager.Register("other_session_connection", otherSessionWebSocket);
        var lobbyMessageHandler = new LobbyMessageHandler(
            sessionManager,
            new DiceService(new FixedDiceRoller(new DiceRoll(3, 3))));
        var handler = new WebSocketConnectionHandler(connectionManager, lobbyMessageHandler);
        using var senderWebSocket = new ScriptedWebSocket(
            TextFrame(JoinMessage(session.SessionId, "player_1")),
            TextFrame(SetReadyMessage(session.SessionId, "player_1", isReady: true)),
            TextFrame(StartGameMessage(session.SessionId, "player_1")),
            TextFrame(RollDiceMessage(session.SessionId, "player_1")),
            CloseFrame());

        await handler.HandleAsync(senderWebSocket, CancellationToken.None);

        Assert.Equal(5, senderWebSocket.SentTextMessages.Count);
        using var senderDirect = JsonDocument.Parse(senderWebSocket.SentTextMessages[3]);
        Assert.Equal("roll_result", senderDirect.RootElement.GetProperty("type").GetString());
        using var senderBroadcast = JsonDocument.Parse(senderWebSocket.SentTextMessages[4]);
        Assert.Equal("dice_rolled", senderBroadcast.RootElement.GetProperty("type").GetString());
        var otherBroadcastMessage = Assert.Single(otherWebSocket.SentTextMessages);
        using var otherBroadcast = JsonDocument.Parse(otherBroadcastMessage);
        Assert.Equal("dice_rolled", otherBroadcast.RootElement.GetProperty("type").GetString());
        Assert.Empty(otherSessionWebSocket.SentTextMessages);
    }

    [Fact]
    public async Task BroadcastFailureForOneTargetDoesNotBlockDirectResponseOrOtherBroadcasts()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_1"), "stale_sender_connection", IsReady: false));
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_2"), "failing_connection", IsReady: true));
        _ = sessionManager.JoinSession(
            session.SessionId,
            new PlayerConnection(new PlayerId("player_3"), "other_connection", IsReady: true));
        var connectionManager = new TestConnectionManager("sender_connection");
        using var failingWebSocket = new ScriptedWebSocket(throwOnSend: true);
        using var otherWebSocket = new ScriptedWebSocket();
        connectionManager.Register("failing_connection", failingWebSocket);
        connectionManager.Register("other_connection", otherWebSocket);
        var lobbyMessageHandler = new LobbyMessageHandler(
            sessionManager,
            new DiceService(new FixedDiceRoller(new DiceRoll(3, 3))));
        var handler = new WebSocketConnectionHandler(connectionManager, lobbyMessageHandler);
        using var senderWebSocket = new ScriptedWebSocket(
            TextFrame(JoinMessage(session.SessionId, "player_1")),
            TextFrame(SetReadyMessage(session.SessionId, "player_1", isReady: true)),
            TextFrame(StartGameMessage(session.SessionId, "player_1")),
            TextFrame(RollDiceMessage(session.SessionId, "player_1")),
            CloseFrame());

        await handler.HandleAsync(senderWebSocket, CancellationToken.None);

        using var senderDirect = JsonDocument.Parse(senderWebSocket.SentTextMessages[3]);
        Assert.Equal("roll_result", senderDirect.RootElement.GetProperty("type").GetString());
        Assert.Equal(1, sessionManager.GetSession(session.SessionId)?.LastEventSequence);
        var otherBroadcastMessage = Assert.Single(otherWebSocket.SentTextMessages);
        using var otherBroadcast = JsonDocument.Parse(otherBroadcastMessage);
        Assert.Equal("dice_rolled", otherBroadcast.RootElement.GetProperty("type").GetString());
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

    private static string SetReadyMessage(string sessionId, string playerId, bool isReady)
    {
        var readyJson = isReady ? "true" : "false";
        return $@"{{""type"":""set_ready"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}"",""isReady"":{readyJson}}}}}";
    }

    private static string StartGameMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""start_game"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string RollDiceMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""roll_dice"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string ResolveTileMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""resolve_tile"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string ExecuteTileMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""execute_tile"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string EndTurnMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""end_turn"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string PlaceBidMessage(string sessionId, string playerId, int amount)
    {
        return $@"{{""type"":""place_bid"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}"",""amount"":{amount}}}}}";
    }

    private static string FinalizeAuctionMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""finalize_auction"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string TakeLoanMessage(string sessionId, string playerId, int amount, string reason)
    {
        return $@"{{""type"":""take_loan"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}"",""amount"":{amount},""reason"":""{reason}""}}}}";
    }

    private static string GetSnapshotMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""get_snapshot"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private sealed record ReceivedFrame(
        WebSocketMessageType MessageType,
        byte[] Payload,
        bool EndOfMessage);

    private sealed class ScriptedWebSocket : WebSocket
    {
        private readonly Queue<ReceivedFrame> frames;
        private readonly bool throwOnSend;
        private WebSocketState state = WebSocketState.Open;

        public ScriptedWebSocket(params ReceivedFrame[] frames)
        {
            this.frames = new Queue<ReceivedFrame>(frames);
        }

        public ScriptedWebSocket(bool throwOnSend, params ReceivedFrame[] frames)
        {
            this.frames = new Queue<ReceivedFrame>(frames);
            this.throwOnSend = throwOnSend;
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
            if (throwOnSend)
            {
                throw new WebSocketException("Scripted send failure.");
            }

            if (messageType == WebSocketMessageType.Text)
            {
                sentTextMessages.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestConnectionManager : IWebSocketConnectionManager
    {
        private readonly string senderConnectionId;
        private readonly Dictionary<string, WebSocketConnection> connections = new();

        public TestConnectionManager(string senderConnectionId)
        {
            this.senderConnectionId = senderConnectionId;
        }

        public int Count => connections.Count;

        public WebSocketConnection Add(WebSocket webSocket)
        {
            var connection = new WebSocketConnection(senderConnectionId, webSocket, DateTimeOffset.UtcNow);
            connections[senderConnectionId] = connection;

            return connection;
        }

        public void Register(string connectionId, WebSocket webSocket)
        {
            connections[connectionId] = new WebSocketConnection(connectionId, webSocket, DateTimeOffset.UtcNow);
        }

        public WebSocketConnection? Get(string connectionId)
        {
            return connections.GetValueOrDefault(connectionId);
        }

        public bool Remove(string connectionId)
        {
            return connections.Remove(connectionId);
        }

        public IReadOnlyCollection<WebSocketConnection> Snapshot()
        {
            return connections.Values.ToArray();
        }
    }

    private sealed class FixedDiceRoller : IDiceRoller
    {
        private readonly DiceRoll diceRoll;

        public FixedDiceRoller(DiceRoll diceRoll)
        {
            this.diceRoll = diceRoll;
        }

        public DiceRoll Roll()
        {
            return diceRoll;
        }
    }
}
