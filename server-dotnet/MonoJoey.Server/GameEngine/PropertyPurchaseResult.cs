namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record PropertyPurchaseResult(
    GameState GameState,
    PlayerId BuyerId,
    TileId PropertyTileId,
    Money PricePaid);
