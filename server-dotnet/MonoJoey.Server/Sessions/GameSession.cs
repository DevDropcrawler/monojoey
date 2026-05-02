namespace MonoJoey.Server.Sessions;

using MonoJoey.Server.GameEngine;

public sealed record GameSession(
    string SessionId,
    IReadOnlyList<PlayerConnection> Players,
    GameState GameState,
    GameSessionStatus Status,
    long LastEventSequence)
{
    public GameRules DraftRules { get; init; } = GameRulesPresets.MonoJoeyDefault;
}
