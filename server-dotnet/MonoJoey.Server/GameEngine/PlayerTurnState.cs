namespace MonoJoey.Server.GameEngine;

public sealed record PlayerTurnState(
    int JailTurnCount,
    int JailRollAttemptCount,
    int ConsecutiveDoublesCount,
    string? LastJailReleaseReason = null)
{
    public static PlayerTurnState Empty { get; } = new(0, 0, 0);
}
