namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record TradeAssets(
    Money Cash,
    IReadOnlyList<TileId> PropertyTileIds);
