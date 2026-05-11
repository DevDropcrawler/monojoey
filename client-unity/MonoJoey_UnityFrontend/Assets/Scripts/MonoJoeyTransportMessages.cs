using System;

public static class MonoJoeyTransportMessageTypes
{
    public const string RollDice = "roll_dice";
    public const string ResolveTile = "resolve_tile";
    public const string ExecuteTile = "execute_tile";
    public const string EndTurn = "end_turn";
    public const string PlaceBid = "place_bid";

    public const string RollResult = "roll_result";
    public const string ResolveTileResult = "resolve_tile_result";
    public const string ExecuteTileResult = "execute_tile_result";
    public const string EndTurnResult = "end_turn_result";
    public const string BidResult = "bid_result";

    public const string SnapshotResult = "snapshot_result";
    public const string ReconnectResult = "reconnect_result";
    public const string Error = "error";
}

public enum MonoJoeyTransportConnectionState
{
    Idle,
    Connecting,
    ConnectedUnbound,
    Reconnecting,
    Hydrating,
    BoundLive,
    Disconnected,
    Error
}

public enum MonoJoeySessionClientMode
{
    MockValidation,
    LiveBackend
}

public interface IMonoJoeyTransport
{
    event Action Connected;
    event Action Disconnected;
    event Action<string> MessageReceived;
    event Action<string> ErrorReceived;
    MonoJoeyTransportConnectionState State { get; }
    bool IsConnected { get; }
    string LastError { get; }
    void Connect(string webSocketUrl);
    void Disconnect();
    void SendJson(string json);
}

[Serializable]
public sealed class MonoJoeyServerEnvelope
{
    public string type;
    public long sequence;
    public string sessionId;
    public string matchId;
    public string createdAtUtc;
}

[Serializable]
public sealed class MonoJoeyErrorEnvelope
{
    public string type;
    public MonoJoeyErrorPayload payload;
}

[Serializable]
public sealed class MonoJoeyErrorPayload
{
    public string code;
    public string message;
}

[Serializable]
public sealed class MonoJoeyReadOnlyRequestEnvelope
{
    public string type;
    public MonoJoeySessionPlayerPayload payload;
}

[Serializable]
public sealed class MonoJoeySessionPlayerPayload
{
    public string sessionId;
    public string playerId;
}

[Serializable]
public sealed class MonoJoeyGameplaySessionPlayerRequestEnvelope
{
    public string type;
    public MonoJoeySessionPlayerPayload payload;
}

[Serializable]
public sealed class MonoJoeyGameplayPlaceBidRequestEnvelope
{
    public string type;
    public MonoJoeyGameplayPlaceBidPayload payload;
}

[Serializable]
public sealed class MonoJoeyGameplayPlaceBidPayload
{
    public string sessionId;
    public string playerId;
    public int amount;
}
