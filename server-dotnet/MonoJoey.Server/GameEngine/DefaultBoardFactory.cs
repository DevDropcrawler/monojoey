namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public static class DefaultBoardFactory
{
    public static Board Create()
    {
        var tiles = new[]
        {
            CreateTile("start", 0, "Start", TileType.Start),
            CreateProperty("property_01", 1, "Placeholder Property 01", "group_01", 60, [2, 10, 30, 90, 160, 250], 50),
            CreateTile("chance_01", 2, "Placeholder Chance 01", TileType.ChanceDeck),
            CreateProperty("property_02", 3, "Placeholder Property 02", "group_01", 60, [4, 20, 60, 180, 320, 450], 50),
            CreateTile("tax_01", 4, "Placeholder Tax 01", TileType.Tax),
            CreateProperty("transport_01", 5, "Placeholder Transport 01", "transport", 200, [25, 50, 100, 200], null, TileType.Transport),
            CreateTile("free_space_01", 6, "Placeholder Free Space 01", TileType.FreeSpace),
            CreateProperty("property_03", 7, "Placeholder Property 03", "group_02", 100, [6, 30, 90, 270, 400, 550], 50),
            CreateTile("table_01", 8, "Placeholder Table 01", TileType.TableDeck),
            CreateProperty("utility_01", 9, "Placeholder Utility 01", "utility", 150, [], null, TileType.Utility),
            CreateTile("lockup_01", 10, "Placeholder Lockup 01", TileType.Lockup),
            CreateTile("go_to_lockup_01", 11, "Placeholder Go To Lockup 01", TileType.GoToLockup),
        };

        return new Board(new BoardId("default_board_v1"), 1, "Default Board V1", tiles);
    }

    private static Tile CreateTile(string id, int index, string displayName, TileType tileType)
    {
        return new Tile(
            new TileId(id),
            index,
            displayName,
            tileType,
            GroupId: null,
            Price: null,
            RentTable: Array.Empty<Money>(),
            UpgradeCost: null,
            IsPurchasable: false,
            IsAuctionable: false);
    }

    private static Tile CreateProperty(
        string id,
        int index,
        string displayName,
        string groupId,
        int price,
        IReadOnlyList<int> rentTable,
        int? upgradeCost,
        TileType tileType = TileType.Property)
    {
        return new Tile(
            new TileId(id),
            index,
            displayName,
            tileType,
            groupId,
            new Money(price),
            rentTable.Select(amount => new Money(amount)).ToArray(),
            upgradeCost is null ? null : new Money(upgradeCost.Value),
            IsPurchasable: true,
            IsAuctionable: true);
    }
}
