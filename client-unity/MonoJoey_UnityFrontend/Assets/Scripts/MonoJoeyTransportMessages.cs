using System;

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
public sealed class MonoJoeyExperimentalDebugSessionPlayerRequestEnvelope
{
    public string type;
    public MonoJoeySessionPlayerPayload payload;
}

[Serializable]
public sealed class MonoJoeyExperimentalDebugPlaceBidRequestEnvelope
{
    public string type;
    public MonoJoeyExperimentalDebugPlaceBidPayload payload;
}

[Serializable]
public sealed class MonoJoeyExperimentalDebugPlaceBidPayload
{
    public string sessionId;
    public string playerId;
    public int amount;
}
