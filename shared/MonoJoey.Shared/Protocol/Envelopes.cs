using System.Text.Json;

namespace MonoJoey.Shared.Protocol;

public sealed record ClientMessageEnvelope(
    MessageId MessageId,
    ClientMessageType Type,
    DateTimeOffset SentAtUtc,
    JsonElement Payload);

public sealed record ServerEventEnvelope(
    EventId EventId,
    long Sequence,
    MatchId? MatchId,
    ServerEventType Type,
    DateTimeOffset CreatedAtUtc,
    JsonElement Payload);
