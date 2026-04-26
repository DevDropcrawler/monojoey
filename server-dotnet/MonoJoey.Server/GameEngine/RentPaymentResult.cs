namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record RentPaymentResult(
    GameState GameState,
    PlayerId LandingPlayerId,
    TileId TileId,
    PlayerId? OwnerId,
    Money RentDue,
    Money RentPaid)
{
    public bool RentCharged => RentPaid.Amount > 0;
}
