namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record RentPaymentResult(
    GameState GameState,
    PlayerId LandingPlayerId,
    TileId TileId,
    PlayerId? OwnerId,
    Money RentDue,
    Money RentPaid,
    PlayerEliminationResult? EliminationResult = null)
{
    public bool RentCharged => RentPaid.Amount > 0;

    public bool PlayerEliminated => EliminationResult?.WasEliminated == true;
}
