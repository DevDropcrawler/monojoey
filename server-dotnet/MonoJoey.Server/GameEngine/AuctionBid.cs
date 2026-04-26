namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record AuctionBid(
    PlayerId BidderId,
    Money Amount,
    DateTimeOffset PlacedAtUtc);
