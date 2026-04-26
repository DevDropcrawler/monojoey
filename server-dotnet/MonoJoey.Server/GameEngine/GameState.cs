namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public sealed record GameState(
    MatchId MatchId,
    GamePhase Phase,
    Board Board,
    IReadOnlyList<Player> Players,
    PlayerId? CurrentTurnPlayerId,
    int TurnNumber,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc);
