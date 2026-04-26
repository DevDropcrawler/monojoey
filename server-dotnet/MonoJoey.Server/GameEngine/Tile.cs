namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public sealed record Tile(
    TileId TileId,
    int Index,
    string DisplayName,
    TileType TileType,
    string? GroupId,
    Money? Price,
    IReadOnlyList<Money> RentTable,
    Money? UpgradeCost,
    bool IsPurchasable,
    bool IsAuctionable);
