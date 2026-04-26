namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class AuctionManagerTests
{
    [Fact]
    public void DefaultConfig_UsesMandatoryAuctionTimerPlaceholders()
    {
        var config = AuctionConfig.Default;

        Assert.True(config.MandatoryAuctionsEnabled);
        Assert.Equal(9, config.InitialPreBidSeconds);
        Assert.Equal(3, config.BidResetSeconds);
        Assert.Equal(new Money(1), config.MinimumBidIncrement);
        Assert.Equal(Money.Zero, config.StartingBid);
    }

    [Fact]
    public void StartMandatoryAuction_StartsForUnownedPropertyWhenEnabled()
    {
        var playerId = new PlayerId("player_1");
        var propertyTileId = new TileId("property_01");
        var gameState = CreateGameState(CreatePlayer("player_1", "property_01"));

        var result = AuctionManager.StartMandatoryAuction(gameState, playerId, propertyTileId);

        Assert.True(result.AuctionStarted);
        Assert.Equal(AuctionStartResultKind.Started, result.ResultKind);
        Assert.NotNull(result.AuctionState);
        Assert.Equal(propertyTileId, result.AuctionState.PropertyTileId);
        Assert.Equal(playerId, result.AuctionState.TriggeringPlayerId);
        Assert.Equal(AuctionStatus.AwaitingInitialBid, result.AuctionState.Status);
        Assert.Equal(Money.Zero, result.AuctionState.StartingBid);
        Assert.Equal(new Money(1), result.AuctionState.MinimumBidIncrement);
        Assert.Equal(9, result.AuctionState.InitialPreBidSeconds);
        Assert.Equal(3, result.AuctionState.BidResetSeconds);
        Assert.Empty(result.AuctionState.Bids);
    }

    [Fact]
    public void StartMandatoryAuction_DoesNotStartWhenMandatoryAuctionsDisabled()
    {
        var config = AuctionConfig.Default with { MandatoryAuctionsEnabled = false };
        var gameState = CreateGameState(CreatePlayer("player_1", "property_01"));

        var result = AuctionManager.StartMandatoryAuction(
            gameState,
            new PlayerId("player_1"),
            new TileId("property_01"),
            config);

        Assert.False(result.AuctionStarted);
        Assert.Equal(AuctionStartResultKind.MandatoryAuctionsDisabled, result.ResultKind);
        Assert.Null(result.AuctionState);
        Assert.Equal("Mandatory auctions are disabled.", result.Message);
    }

    [Fact]
    public void StartMandatoryAuction_DoesNotStartForOwnedProperty()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", "property_01"),
            CreatePlayer("player_2", "start", 1500, "property_01"));

        var result = AuctionManager.StartMandatoryAuction(
            gameState,
            new PlayerId("player_1"),
            new TileId("property_01"));

        Assert.False(result.AuctionStarted);
        Assert.Equal(AuctionStartResultKind.PropertyAlreadyOwned, result.ResultKind);
        Assert.Null(result.AuctionState);
        Assert.Equal("Property is already owned.", result.Message);
    }

    [Fact]
    public void StartMandatoryAuction_DoesNotStartForNonPropertyTile()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "free_space_01"));

        var result = AuctionManager.StartMandatoryAuction(
            gameState,
            new PlayerId("player_1"),
            new TileId("free_space_01"));

        Assert.False(result.AuctionStarted);
        Assert.Equal(AuctionStartResultKind.TileNotAuctionable, result.ResultKind);
        Assert.Null(result.AuctionState);
        Assert.Equal("Tile is not eligible for a mandatory auction.", result.Message);
    }

    [Fact]
    public void StartMandatoryAuction_RejectsUnknownPlayer()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "property_01"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => AuctionManager.StartMandatoryAuction(
                gameState,
                new PlayerId("missing_player"),
                new TileId("property_01")));

        Assert.Equal("Auction triggering player must exist in the game player list.", exception.Message);
    }

    [Fact]
    public void StartMandatoryAuction_RejectsUnknownTile()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "property_01"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => AuctionManager.StartMandatoryAuction(
                gameState,
                new PlayerId("player_1"),
                new TileId("missing_property")));

        Assert.Equal("Auction tile must exist on the board.", exception.Message);
    }

    private static GameState CreateGameState(params Player[] players)
    {
        return new GameState(
            new MatchId("match_123"),
            GamePhase.AwaitingRoll,
            DefaultBoardFactory.Create(),
            players,
            players[0].PlayerId,
            TurnNumber: 1,
            DateTimeOffset.Parse("2026-04-26T00:00:00+00:00"),
            EndedAtUtc: null);
    }

    private static Player CreatePlayer(
        string playerId,
        string currentTileId,
        int money = 1500,
        params string[] ownedPropertyIds)
    {
        return new Player(
            new PlayerId(playerId),
            playerId,
            $"token_{playerId}",
            $"color_{playerId}",
            new Money(money),
            new TileId(currentTileId),
            ownedPropertyIds.Select(propertyId => new TileId(propertyId)).ToHashSet(),
            new HashSet<CardId>(),
            IsBankrupt: false,
            IsEliminated: false);
    }
}
