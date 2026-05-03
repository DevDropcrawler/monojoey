namespace MonoJoey.Server.Tests.Realtime;

using System.Text.Json;
using MonoJoey.Server.Realtime;
using MonoJoey.Server.Sessions;

public class LobbyRulesMessageHandlerTests
{
    [Fact]
    public void LobbyState_IncludesDraftRules()
    {
        var sessionManager = new SessionManager();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, @"{""type"":""create_lobby""}");
        var payload = AssertResponseType(response, "lobby_state");
        var rules = payload.GetProperty("rules");

        Assert.Equal("monojoey_default", rules.GetProperty("presetId").GetString());
        Assert.Equal("MonoJoey default", rules.GetProperty("presetName").GetString());
        Assert.False(rules.GetProperty("isCustom").GetBoolean());
        Assert.Equal(100, rules.GetProperty("economy").GetProperty("incomeTaxAmount").GetInt32());
        Assert.Equal(100, rules.GetProperty("economy").GetProperty("luxuryTaxAmount").GetInt32());
        Assert.Equal(9, rules.GetProperty("auction").GetProperty("initialTimerSeconds").GetInt32());
        Assert.False(rules.GetProperty("dice").GetProperty("doublesExtraTurnEnabled").GetBoolean());
        Assert.Equal(3, rules.GetProperty("dice").GetProperty("maxConsecutiveDoublesBeforeLockup").GetInt32());
        Assert.Equal(new[] { "chance", "table" }, rules.GetProperty("cards").GetProperty("decksEnabled").EnumerateArray().Select(deck => deck.GetString()).ToArray());
    }

    [Fact]
    public void SetRules_BroadcastsRulesUpdatedWithoutAllocatingSequence()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var firstContext = new LobbyConnectionContext("connection_1");
        var secondContext = new LobbyConnectionContext("connection_2");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), firstContext);
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_2"), secondContext);

        var result = handler.HandleTextMessageResult(
            SetRulesMessage(
                session.SessionId,
                "player_1",
                @"""presetName"":""House rules"",""auction"":{""initialTimerSeconds"":12,""minimumBidIncrement"":5},""dice"":{""doublesExtraTurnEnabled"":true,""maxConsecutiveDoublesBeforeLockup"":2},""loans"":{""loanSharkEnabled"":false}"),
            firstContext);

        Assert.Equal("rules_updated", result.DirectResponse.Type);
        Assert.NotNull(result.Broadcast);
        Assert.Equal("rules_updated", result.Broadcast.Type);
        Assert.Equal(0, result.Broadcast.Sequence);
        Assert.Equal(session.SessionId, result.Broadcast.SessionId);
        Assert.Equal(new[] { "connection_1", "connection_2" }, result.BroadcastConnectionIds);
        Assert.Equal(0, sessionManager.GetSession(session.SessionId)?.LastEventSequence);
        var payload = Assert.IsType<RulesUpdatedPayload>(result.Broadcast.Payload);
        Assert.Equal(session.SessionId, payload.SessionId);
        Assert.Equal("custom", payload.Rules.PresetId);
        Assert.Equal("House rules", payload.Rules.PresetName);
        Assert.True(payload.Rules.IsCustom);
        Assert.Equal(12, payload.Rules.Auction.InitialTimerSeconds);
        Assert.Equal(5, payload.Rules.Auction.MinimumBidIncrement);
        Assert.True(payload.Rules.Dice.DoublesExtraTurnEnabled);
        Assert.Equal(2, payload.Rules.Dice.MaxConsecutiveDoublesBeforeLockup);
        Assert.False(payload.Rules.Loans.LoanSharkEnabled);
    }

    [Fact]
    public void SetRules_DirectResponseReturnsResolvedAuthoritativeRules()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(
            handler,
            context,
            SetRulesMessage(session.SessionId, "player_1", @"""auction"":{""initialTimerSeconds"":12}"));
        var payload = AssertResponseType(response, "rules_updated");
        var rules = payload.GetProperty("rules");

        Assert.Equal(session.SessionId, payload.GetProperty("sessionId").GetString());
        Assert.Equal("custom", rules.GetProperty("presetId").GetString());
        Assert.True(rules.GetProperty("isCustom").GetBoolean());
        Assert.Equal(12, rules.GetProperty("auction").GetProperty("initialTimerSeconds").GetInt32());
        Assert.Equal(3, rules.GetProperty("auction").GetProperty("bidResetTimerSeconds").GetInt32());
        Assert.Equal(1500, rules.GetProperty("economy").GetProperty("startingMoney").GetInt32());
        Assert.Equal(100, rules.GetProperty("economy").GetProperty("incomeTaxAmount").GetInt32());
        Assert.Equal(100, rules.GetProperty("economy").GetProperty("luxuryTaxAmount").GetInt32());
    }

    [Theory]
    [InlineData(@"""presetId"":""missing""")]
    [InlineData(@"""unknown"":{}")]
    [InlineData(@"""auction"":{""unknown"":1}")]
    [InlineData(@"""auction"":{""initialTimerSeconds"":0}")]
    [InlineData(@"""economy"":{""incomeTaxAmount"":-1}")]
    [InlineData(@"""economy"":{""luxuryTaxAmount"":-1}")]
    [InlineData(@"""dice"":{""maxConsecutiveDoublesBeforeLockup"":0}")]
    [InlineData(@"""dice"":{""doublesExtraTurnEnabled"":""yes""}")]
    [InlineData(@"""loans"":{""baseInterestRate"":-0.1}")]
    [InlineData(@"""cards"":{""decksEnabled"":[""unknown""]}")]
    public void SetRules_InvalidRulesReturnsInvalidRules(string rulesJson)
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), context);

        using var response = Handle(
            handler,
            context,
            SetRulesMessage(session.SessionId, "player_1", rulesJson));

        AssertError(response, "invalid_rules");
    }

    [Fact]
    public void SetRules_RequiresBoundSessionAndPlayer()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(
            handler,
            context,
            SetRulesMessage(session.SessionId, "player_1", @"""auction"":{""initialTimerSeconds"":12}"));

        AssertError(response, "player_switch_rejected");
    }

    [Theory]
    [InlineData(@"{""type"":""set_rules"",""payload"":{""playerId"":""player_1"",""rules"":{}}}")]
    [InlineData(@"{""type"":""set_rules"",""payload"":{""sessionId"":""session_1"",""rules"":{}}}")]
    [InlineData(@"{""type"":""set_rules"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1""}}")]
    [InlineData(@"{""type"":""set_rules"",""payload"":{""sessionId"":""session_1"",""playerId"":""player_1"",""rules"":[]}}")]
    public void SetRules_InvalidEnvelopeReturnsInvalidPayload(string message)
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, message);

        AssertError(response, "invalid_payload");
    }

    [Fact]
    public void SetRules_AfterGameStartReturnsInvalidSessionStatus()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var firstContext = new LobbyConnectionContext("connection_1");
        var secondContext = new LobbyConnectionContext("connection_2");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), firstContext);
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_2"), secondContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_1", isReady: true), firstContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_2", isReady: true), secondContext);
        _ = handler.HandleTextMessage(StartGameMessage(session.SessionId, "player_1"), firstContext);

        using var response = Handle(
            handler,
            firstContext,
            SetRulesMessage(session.SessionId, "player_1", @"""auction"":{""initialTimerSeconds"":12}"));

        AssertError(response, "invalid_session_status");
    }

    [Fact]
    public void SnapshotAndReconnect_IncludeStartedGameRules()
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.CreateSession();
        var handler = new LobbyMessageHandler(sessionManager);
        var firstContext = new LobbyConnectionContext("connection_1");
        var secondContext = new LobbyConnectionContext("connection_2");
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_1"), firstContext);
        _ = handler.HandleTextMessage(JoinMessage(session.SessionId, "player_2"), secondContext);
        _ = handler.HandleTextMessage(
            SetRulesMessage(session.SessionId, "player_1", @"""presetName"":""House rules"",""economy"":{""incomeTaxAmount"":75,""luxuryTaxAmount"":25},""auction"":{""initialTimerSeconds"":12},""dice"":{""doublesExtraTurnEnabled"":true,""maxConsecutiveDoublesBeforeLockup"":2}"),
            firstContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_1", isReady: true), firstContext);
        _ = handler.HandleTextMessage(SetReadyMessage(session.SessionId, "player_2", isReady: true), secondContext);
        _ = handler.HandleTextMessage(StartGameMessage(session.SessionId, "player_1"), firstContext);
        var reconnectContext = new LobbyConnectionContext("connection_reconnect");

        using var snapshotResponse = Handle(handler, firstContext, GetSnapshotMessage(session.SessionId, "player_1"));
        using var reconnectResponse = Handle(handler, reconnectContext, ReconnectMessage(session.SessionId, "player_1"));
        var snapshotRules = AssertResponseType(snapshotResponse, "snapshot_result").GetProperty("rules");
        var reconnectRules = AssertResponseType(reconnectResponse, "reconnect_result")
            .GetProperty("snapshot")
            .GetProperty("rules");

        Assert.Equal("custom", snapshotRules.GetProperty("presetId").GetString());
        Assert.Equal("House rules", snapshotRules.GetProperty("presetName").GetString());
        Assert.Equal(75, snapshotRules.GetProperty("economy").GetProperty("incomeTaxAmount").GetInt32());
        Assert.Equal(25, snapshotRules.GetProperty("economy").GetProperty("luxuryTaxAmount").GetInt32());
        Assert.Equal(12, snapshotRules.GetProperty("auction").GetProperty("initialTimerSeconds").GetInt32());
        Assert.True(snapshotRules.GetProperty("dice").GetProperty("doublesExtraTurnEnabled").GetBoolean());
        Assert.Equal(2, snapshotRules.GetProperty("dice").GetProperty("maxConsecutiveDoublesBeforeLockup").GetInt32());
        Assert.Equal("custom", reconnectRules.GetProperty("presetId").GetString());
        Assert.Equal("House rules", reconnectRules.GetProperty("presetName").GetString());
        Assert.Equal(75, reconnectRules.GetProperty("economy").GetProperty("incomeTaxAmount").GetInt32());
        Assert.Equal(25, reconnectRules.GetProperty("economy").GetProperty("luxuryTaxAmount").GetInt32());
        Assert.Equal(12, reconnectRules.GetProperty("auction").GetProperty("initialTimerSeconds").GetInt32());
        Assert.True(reconnectRules.GetProperty("dice").GetProperty("doublesExtraTurnEnabled").GetBoolean());
        Assert.Equal(2, reconnectRules.GetProperty("dice").GetProperty("maxConsecutiveDoublesBeforeLockup").GetInt32());
    }

    [Fact]
    public void ClientSentRulesUpdatedReturnsUnsupportedMessage()
    {
        var handler = new LobbyMessageHandler(new SessionManager());
        var context = new LobbyConnectionContext("connection_1");

        using var response = Handle(handler, context, @"{""type"":""rules_updated""}");

        AssertError(response, "unsupported_message");
    }

    private static JsonDocument Handle(
        LobbyMessageHandler handler,
        LobbyConnectionContext context,
        string message)
    {
        return JsonDocument.Parse(handler.HandleTextMessage(message, context));
    }

    private static JsonElement AssertResponseType(JsonDocument response, string type)
    {
        var root = response.RootElement;

        Assert.Equal(type, root.GetProperty("type").GetString());
        Assert.True(root.TryGetProperty("payload", out var payload));

        return payload;
    }

    private static void AssertError(JsonDocument response, string code)
    {
        var payload = AssertResponseType(response, "error");

        Assert.Equal(code, payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("message").GetString()));
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

    private static string SetRulesMessage(string sessionId, string playerId, string rulesJson)
    {
        return $@"{{""type"":""set_rules"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}"",""rules"":{{{rulesJson}}}}}}}";
    }

    private static string StartGameMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""start_game"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string GetSnapshotMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""get_snapshot"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }

    private static string ReconnectMessage(string sessionId, string playerId)
    {
        return $@"{{""type"":""reconnect_session"",""payload"":{{""sessionId"":""{sessionId}"",""playerId"":""{playerId}""}}}}";
    }
}
