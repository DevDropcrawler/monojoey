namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Schemas;

public class DefaultBoardFactoryTests
{
    [Fact]
    public void Create_ReturnsPlaceholderBoardWithStartTileAtIndexZero()
    {
        var board = DefaultBoardFactory.Create();

        Assert.Equal("default_board_v1", board.BoardId.Value);
        Assert.NotEmpty(board.Tiles);
        Assert.Equal(TileType.Start, board.Tiles[0].TileType);
        Assert.Equal(0, board.Tiles[0].Index);
    }

    [Fact]
    public void Create_ReturnsTilesWithUniqueIdsAndSequentialIndexes()
    {
        var board = DefaultBoardFactory.Create();

        Assert.Equal(board.Tiles.Count, board.Tiles.Select(tile => tile.TileId).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, board.Tiles.Count), board.Tiles.Select(tile => tile.Index));
    }

    [Fact]
    public void Create_ReturnsPlaceholderPurchasableTilesWithPrices()
    {
        var board = DefaultBoardFactory.Create();
        var purchasableTiles = board.Tiles.Where(tile => tile.IsPurchasable).ToArray();

        Assert.NotEmpty(purchasableTiles);
        Assert.All(purchasableTiles, tile =>
        {
            Assert.True(tile.IsAuctionable);
            Assert.NotNull(tile.Price);
            Assert.True(tile.Price.Value.Amount > 0);
        });
    }
}
