namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class AuctionManagerTests
{
    private static readonly DateTimeOffset FirstBidTime = DateTimeOffset.Parse("2026-04-26T01:00:00+00:00");
    private static readonly DateTimeOffset SecondBidTime = DateTimeOffset.Parse("2026-04-26T01:01:00+00:00");

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
        Assert.Null(result.AuctionState.HighestBid);
        Assert.Null(result.AuctionState.HighestBidderId);
        Assert.Null(result.AuctionState.CountdownDurationSeconds);
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

    [Fact]
    public void PlaceBid_AcceptsFirstValidBid()
    {
        var bidderId = new PlayerId("player_2");
        var config = AuctionConfig.Default with { StartingBid = new Money(100) };
        var gameState = CreateGameState(
            CreatePlayer("player_1", "property_01"),
            CreatePlayer("player_2", "start"));
        var auctionState = StartAuction(gameState, config);

        var result = AuctionManager.PlaceBid(gameState, auctionState, bidderId, new Money(100), FirstBidTime);

        Assert.True(result.BidAccepted);
        Assert.Equal(AuctionBidResultKind.Accepted, result.ResultKind);
        Assert.Equal(AuctionStatus.ActiveBidCountdown, result.AuctionState.Status);
        Assert.Equal(new Money(100), result.AuctionState.HighestBid);
        Assert.Equal(bidderId, result.AuctionState.HighestBidderId);
        Assert.Equal(3, result.AuctionState.CountdownDurationSeconds);
        var bid = Assert.Single(result.AuctionState.Bids);
        Assert.Equal(bidderId, bid.BidderId);
        Assert.Equal(new Money(100), bid.Amount);
        Assert.Equal(FirstBidTime, bid.PlacedAtUtc);
    }

    [Fact]
    public void PlaceBid_RejectsFirstBidBelowStartingBid()
    {
        var config = AuctionConfig.Default with { StartingBid = new Money(100) };
        var gameState = CreateGameState(CreatePlayer("player_1", "property_01"));
        var auctionState = StartAuction(gameState, config);

        var result = AuctionManager.PlaceBid(
            gameState,
            auctionState,
            new PlayerId("player_1"),
            new Money(99),
            FirstBidTime);

        Assert.False(result.BidAccepted);
        Assert.Equal(AuctionBidResultKind.BidBelowStartingBid, result.ResultKind);
        Assert.Same(auctionState, result.AuctionState);
        Assert.Empty(result.AuctionState.Bids);
    }

    [Fact]
    public void PlaceBid_RejectsLaterBidBelowMinimumIncrement()
    {
        var config = AuctionConfig.Default with { MinimumBidIncrement = new Money(5) };
        var gameState = CreateGameState(
            CreatePlayer("player_1", "property_01"),
            CreatePlayer("player_2", "start"));
        var auctionState = StartAuction(gameState, config);
        var firstBid = AuctionManager.PlaceBid(
            gameState,
            auctionState,
            new PlayerId("player_1"),
            new Money(10),
            FirstBidTime).AuctionState;

        var result = AuctionManager.PlaceBid(
            gameState,
            firstBid,
            new PlayerId("player_2"),
            new Money(14),
            SecondBidTime);

        Assert.False(result.BidAccepted);
        Assert.Equal(AuctionBidResultKind.BidBelowMinimumIncrement, result.ResultKind);
        Assert.Same(firstBid, result.AuctionState);
        Assert.Equal(new Money(10), result.AuctionState.HighestBid);
        Assert.Single(result.AuctionState.Bids);
    }

    [Fact]
    public void PlaceBid_ValidLaterBidUpdatesHighestBidderAndBid()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", "property_01"),
            CreatePlayer("player_2", "start"));
        var auctionState = StartAuction(gameState);
        var firstBid = AuctionManager.PlaceBid(
            gameState,
            auctionState,
            new PlayerId("player_1"),
            new Money(10),
            FirstBidTime).AuctionState;

        var result = AuctionManager.PlaceBid(
            gameState,
            firstBid,
            new PlayerId("player_2"),
            new Money(11),
            SecondBidTime);

        Assert.True(result.BidAccepted);
        Assert.Equal(new Money(11), result.AuctionState.HighestBid);
        Assert.Equal(new PlayerId("player_2"), result.AuctionState.HighestBidderId);
        Assert.Equal(2, result.AuctionState.Bids.Count);
    }

    [Fact]
    public void PlaceBid_ValidBidResetsCountdownMetadataToDefaultBidResetSeconds()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", "property_01"),
            CreatePlayer("player_2", "start"));
        var auctionState = StartAuction(gameState);
        var firstBid = AuctionManager.PlaceBid(
            gameState,
            auctionState,
            new PlayerId("player_1"),
            new Money(10),
            FirstBidTime).AuctionState with
        {
            CountdownDurationSeconds = 1,
        };

        var result = AuctionManager.PlaceBid(
            gameState,
            firstBid,
            new PlayerId("player_2"),
            new Money(11),
            SecondBidTime);

        Assert.True(result.BidAccepted);
        Assert.Equal(3, result.AuctionState.CountdownDurationSeconds);
    }

    [Fact]
    public void PlaceBid_RejectsBidderNotInGame()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "property_01"));
        var auctionState = StartAuction(gameState);

        var result = AuctionManager.PlaceBid(
            gameState,
            auctionState,
            new PlayerId("missing_player"),
            new Money(10),
            FirstBidTime);

        Assert.False(result.BidAccepted);
        Assert.Equal(AuctionBidResultKind.BidderNotInGame, result.ResultKind);
        Assert.Same(auctionState, result.AuctionState);
        Assert.Empty(result.AuctionState.Bids);
    }

    [Fact]
    public void PlaceBid_RejectsEliminatedPlayer()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", "property_01"),
            CreateEliminatedPlayer("player_2", "start"));
        var auctionState = StartAuction(gameState);

        var result = AuctionManager.PlaceBid(
            gameState,
            auctionState,
            new PlayerId("player_2"),
            new Money(10),
            FirstBidTime);

        Assert.False(result.BidAccepted);
        Assert.Equal(AuctionBidResultKind.BidderEliminated, result.ResultKind);
        Assert.Same(auctionState, result.AuctionState);
        Assert.Empty(result.AuctionState.Bids);
    }

    [Fact]
    public void PlaceBid_PreservesBidHistory()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", "property_01"),
            CreatePlayer("player_2", "start"));
        var auctionState = StartAuction(gameState);
        var firstBid = AuctionManager.PlaceBid(
            gameState,
            auctionState,
            new PlayerId("player_1"),
            new Money(10),
            FirstBidTime).AuctionState;

        var result = AuctionManager.PlaceBid(
            gameState,
            firstBid,
            new PlayerId("player_2"),
            new Money(11),
            SecondBidTime);

        Assert.Collection(
            result.AuctionState.Bids,
            bid =>
            {
                Assert.Equal(new PlayerId("player_1"), bid.BidderId);
                Assert.Equal(new Money(10), bid.Amount);
                Assert.Equal(FirstBidTime, bid.PlacedAtUtc);
            },
            bid =>
            {
                Assert.Equal(new PlayerId("player_2"), bid.BidderId);
                Assert.Equal(new Money(11), bid.Amount);
                Assert.Equal(SecondBidTime, bid.PlacedAtUtc);
            });
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

    private static Player CreateEliminatedPlayer(string playerId, string currentTileId)
    {
        return CreatePlayer(playerId, currentTileId) with
        {
            IsBankrupt = true,
            IsEliminated = true,
        };
    }

    private static AuctionState StartAuction(GameState gameState, AuctionConfig? config = null)
    {
        var result = AuctionManager.StartMandatoryAuction(
            gameState,
            new PlayerId("player_1"),
            new TileId("property_01"),
            config);

        Assert.True(result.AuctionStarted);
        Assert.NotNull(result.AuctionState);
        return result.AuctionState;
    }
}
