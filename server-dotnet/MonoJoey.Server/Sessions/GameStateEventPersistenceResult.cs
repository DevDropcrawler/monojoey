namespace MonoJoey.Server.Sessions;

public sealed record GameStateEventPersistenceResult(
    GameSession Session,
    long Sequence,
    long? CompletionSequence = null);
