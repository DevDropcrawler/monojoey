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
    DateTimeOffset? EndedAtUtc)
{
    public bool HasRolledThisTurn { get; init; }

    public bool HasResolvedTileThisTurn { get; init; }

    public bool HasExecutedTileThisTurn { get; init; }

    public AuctionState? ActiveAuctionState { get; init; }

    public LoanSharkConfig LoanSharkConfig { get; init; } = new();

    public IReadOnlyDictionary<string, CardDeckState> CardDeckStates { get; init; } =
        new Dictionary<string, CardDeckState>();
}
