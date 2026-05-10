namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record RentAssessmentResult(
    GameState GameState,
    PlayerId LandingPlayerId,
    TileId TileId,
    PlayerId? OwnerId,
    Money RentDue)
{
    public bool PaymentRequired => OwnerId is not null && RentDue.Amount > 0;
}
