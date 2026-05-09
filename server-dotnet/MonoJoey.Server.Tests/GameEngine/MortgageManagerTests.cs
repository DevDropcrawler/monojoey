namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class MortgageManagerTests
{
    [Fact]
    public void MortgageProperty_AddsMortgageValueAndMarksPropertyMortgaged()
    {
        var propertyTileId = new TileId("property_03");
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", "start", 1500, "property_03"));

        var result = MortgageManager.MortgageProperty(gameState, playerId, propertyTileId);

        Assert.True(result.MortgageAccepted);
        Assert.Equal(new Money(50), result.MortgageValue);
        Assert.Equal(new Money(1550), result.Money);
        Assert.Equal(new Money(1550), result.GameState.Players[0].Money);
        Assert.True(result.GameState.PropertyStates[propertyTileId].Data.IsMortgaged);
        Assert.Equal(0, result.GameState.PropertyStates[propertyTileId].Data.DamagePercent);
    }

    [Fact]
    public void MortgageProperty_PreservesDamageState()
    {
        var propertyTileId = new TileId("property_03");
        var gameState = CreateGameState(CreatePlayer("player_1", "start", 1500, "property_03")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [propertyTileId] = new(propertyTileId, new PropertyStateData(25)),
            },
        };

        var result = MortgageManager.MortgageProperty(gameState, new PlayerId("player_1"), propertyTileId);

        Assert.True(result.MortgageAccepted);
        Assert.True(result.GameState.PropertyStates[propertyTileId].Data.IsMortgaged);
        Assert.Equal(25, result.GameState.PropertyStates[propertyTileId].Data.DamagePercent);
    }

    [Fact]
    public void MortgageProperty_RejectsUpgradedProperty()
    {
        var propertyTileId = new TileId("property_03");
        var gameState = CreateGameState(CreatePlayer("player_1", "start", 1500, "property_03")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [propertyTileId] = new(
                    propertyTileId,
                    new PropertyStateData(upgradeLevel: 4)),
            },
        };

        var result = MortgageManager.MortgageProperty(gameState, new PlayerId("player_1"), propertyTileId);

        Assert.Equal(MortgageResultKind.PropertyHasUpgrades, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Theory]
    [InlineData("property_02", MortgageResultKind.PropertyNotOwned)]
    [InlineData("free_space_01", MortgageResultKind.InvalidProperty)]
    [InlineData("missing_property", MortgageResultKind.InvalidProperty)]
    public void MortgageProperty_RejectsInvalidPropertyRequests(string propertyTileId, MortgageResultKind expectedKind)
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "start", 1500, "property_03"));

        var result = MortgageManager.MortgageProperty(
            gameState,
            new PlayerId("player_1"),
            new TileId(propertyTileId));

        Assert.Equal(expectedKind, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void MortgageProperty_RejectsAlreadyMortgagedProperty()
    {
        var propertyTileId = new TileId("property_03");
        var gameState = CreateGameState(CreatePlayer("player_1", "start", 1500, "property_03")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [propertyTileId] = new(propertyTileId, new PropertyStateData(isMortgaged: true)),
            },
        };

        var result = MortgageManager.MortgageProperty(gameState, new PlayerId("player_1"), propertyTileId);

        Assert.Equal(MortgageResultKind.AlreadyMortgaged, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Theory]
    [InlineData("disabled", MortgageResultKind.MortgagesDisabled)]
    [InlineData("auction", MortgageResultKind.ActiveAuction)]
    [InlineData("unresolved_tile", MortgageResultKind.UnresolvedTileExecution)]
    [InlineData("completed", MortgageResultKind.GameNotInProgress)]
    public void MortgageProperty_RejectsBlockedSessionStates(string scenario, MortgageResultKind expectedKind)
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "start", 1500, "property_03"));
        gameState = scenario switch
        {
            "disabled" => gameState with
            {
                Rules = gameState.Rules with
                {
                    Economy = gameState.Rules.Economy with { MortgagesEnabled = false },
                },
            },
            "auction" => gameState with { ActiveAuctionState = CreateAuctionState() },
            "unresolved_tile" => gameState with
            {
                HasRolledThisTurn = true,
                HasResolvedTileThisTurn = true,
                HasExecutedTileThisTurn = false,
            },
            "completed" => gameState with { Status = GameStatus.Completed },
            _ => gameState,
        };

        var result = MortgageManager.MortgageProperty(
            gameState,
            new PlayerId("player_1"),
            new TileId("property_03"));

        Assert.Equal(expectedKind, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void UnmortgageProperty_DeductsCostAndRemovesCleanPropertyState()
    {
        var propertyTileId = new TileId("property_03");
        var gameState = CreateGameState(CreatePlayer("player_1", "start", 1500, "property_03")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [propertyTileId] = new(propertyTileId, new PropertyStateData(isMortgaged: true)),
            },
        };

        var result = MortgageManager.UnmortgageProperty(gameState, new PlayerId("player_1"), propertyTileId);

        Assert.True(result.UnmortgageAccepted);
        Assert.Equal(new Money(50), result.MortgageValue);
        Assert.Equal(new Money(5), result.UnmortgageInterest);
        Assert.Equal(new Money(55), result.UnmortgageCost);
        Assert.Equal(new Money(1445), result.Money);
        Assert.DoesNotContain(propertyTileId, result.GameState.PropertyStates.Keys);
    }

    [Fact]
    public void UnmortgageProperty_PreservesDamageState()
    {
        var propertyTileId = new TileId("property_03");
        var gameState = CreateGameState(CreatePlayer("player_1", "start", 1500, "property_03")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [propertyTileId] = new(propertyTileId, new PropertyStateData(40, isMortgaged: true)),
            },
        };

        var result = MortgageManager.UnmortgageProperty(gameState, new PlayerId("player_1"), propertyTileId);

        Assert.True(result.UnmortgageAccepted);
        Assert.False(result.GameState.PropertyStates[propertyTileId].Data.IsMortgaged);
        Assert.Equal(40, result.GameState.PropertyStates[propertyTileId].Data.DamagePercent);
    }

    [Fact]
    public void UnmortgageProperty_PreservesUpgradeLevelOnCleanProperty()
    {
        var propertyTileId = new TileId("property_03");
        var gameState = CreateGameState(CreatePlayer("player_1", "start", 1500, "property_03")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [propertyTileId] = new(
                    propertyTileId,
                    new PropertyStateData(isMortgaged: true, upgradeLevel: 2)),
            },
        };

        var result = MortgageManager.UnmortgageProperty(gameState, new PlayerId("player_1"), propertyTileId);

        Assert.True(result.UnmortgageAccepted);
        Assert.False(result.GameState.PropertyStates[propertyTileId].Data.IsMortgaged);
        Assert.Equal(0, result.GameState.PropertyStates[propertyTileId].Data.DamagePercent);
        Assert.Equal(2, result.GameState.PropertyStates[propertyTileId].Data.UpgradeLevel);
    }

    [Fact]
    public void UnmortgageProperty_RejectsInsufficientCashWithoutMutation()
    {
        var propertyTileId = new TileId("property_03");
        var gameState = CreateGameState(CreatePlayer("player_1", "start", 54, "property_03")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [propertyTileId] = new(propertyTileId, new PropertyStateData(isMortgaged: true)),
            },
        };

        var result = MortgageManager.UnmortgageProperty(gameState, new PlayerId("player_1"), propertyTileId);

        Assert.Equal(UnmortgageResultKind.InsufficientCash, result.ResultKind);
        Assert.Equal(new Money(55), result.UnmortgageCost);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void UnmortgageProperty_RejectsNotMortgagedProperty()
    {
        var propertyTileId = new TileId("property_03");
        var gameState = CreateGameState(CreatePlayer("player_1", "start", 1500, "property_03"));

        var result = MortgageManager.UnmortgageProperty(gameState, new PlayerId("player_1"), propertyTileId);

        Assert.Equal(UnmortgageResultKind.NotMortgaged, result.ResultKind);
        Assert.Same(gameState, result.GameState);
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
        int money,
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

    private static AuctionState CreateAuctionState()
    {
        return new AuctionState(
            new TileId("property_01"),
            new PlayerId("player_1"),
            AuctionStatus.AwaitingInitialBid,
            Money.Zero,
            new Money(1),
            InitialPreBidSeconds: 9,
            BidResetSeconds: 3,
            Array.Empty<AuctionBid>(),
            HighestBid: null,
            HighestBidderId: null,
            CountdownDurationSeconds: 9,
            TimerEndsAtUtc: DateTimeOffset.Parse("2026-04-26T00:00:09+00:00"));
    }
}
