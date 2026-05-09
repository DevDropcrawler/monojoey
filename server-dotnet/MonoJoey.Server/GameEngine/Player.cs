namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record Player(
    PlayerId PlayerId,
    string Username,
    string TokenId,
    string ColorId,
    Money Money,
    TileId CurrentTileId,
    IReadOnlySet<TileId> OwnedPropertyIds,
    IReadOnlySet<CardId> HeldCardIds,
    bool IsBankrupt,
    bool IsEliminated)
{
    public PlayerLoanState? LoanState { get; init; }

    public IReadOnlyList<PlayerStatusEffect> StatusEffects { get; init; } = Array.Empty<PlayerStatusEffect>();

    public PlayerTurnState TurnState { get; init; } = PlayerTurnState.Empty;

    public bool IsLockedUp { get; init; }
}
