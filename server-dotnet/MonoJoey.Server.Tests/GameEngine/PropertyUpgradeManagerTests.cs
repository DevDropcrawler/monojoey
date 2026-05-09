namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class PropertyUpgradeManagerTests
{
    [Fact]
    public void BuyUpgrade_DeductsCostAndIncrementsUpgradeLevel()
    {
        var propertyTileId = new TileId("property_01");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01", "property_02"),
            CreatePlayer("player_2", 1500));

        var result = PropertyUpgradeManager.BuyUpgrade(gameState, new PlayerId("player_1"), propertyTileId);

        Assert.True(result.UpgradeBought);
        Assert.Equal(new Money(50), result.UpgradeCost);
        Assert.Equal(new Money(1450), result.Money);
        Assert.Equal(1, result.UpgradeLevel);
        Assert.Equal(1, result.GameState.PropertyStates[propertyTileId].Data.UpgradeLevel);
        Assert.Empty(gameState.PropertyStates);
    }

    [Theory]
    [InlineData("disabled", PropertyUpgradeResultKind.UpgradesDisabled)]
    [InlineData("auction", PropertyUpgradeResultKind.ActiveAuction)]
    [InlineData("unresolved_tile", PropertyUpgradeResultKind.UnresolvedTileExecution)]
    [InlineData("completed", PropertyUpgradeResultKind.GameNotInProgress)]
    public void BuyUpgrade_RejectsBlockedGameStates(string scenario, PropertyUpgradeResultKind expectedKind)
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01", "property_02"),
            CreatePlayer("player_2", 1500));
        gameState = scenario switch
        {
            "disabled" => gameState with
            {
                Rules = gameState.Rules with
                {
                    Economy = gameState.Rules.Economy with { UpgradesEnabled = false },
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

        var result = PropertyUpgradeManager.BuyUpgrade(
            gameState,
            new PlayerId("player_1"),
            new TileId("property_01"));

        Assert.Equal(expectedKind, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Theory]
    [InlineData("missing_player", PropertyUpgradeResultKind.PlayerNotInGame)]
    [InlineData("bankrupt", PropertyUpgradeResultKind.PlayerBankrupt)]
    [InlineData("eliminated", PropertyUpgradeResultKind.PlayerEliminated)]
    public void BuyUpgrade_RejectsInvalidPlayers(string scenario, PropertyUpgradeResultKind expectedKind)
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01", "property_02"),
            CreatePlayer("player_2", 1500));
        gameState = scenario switch
        {
            "bankrupt" => gameState with
            {
                Players = gameState.Players
                    .Select(player => player.PlayerId == playerId ? player with { IsBankrupt = true } : player)
                    .ToArray(),
            },
            "eliminated" => gameState with
            {
                Players = gameState.Players
                    .Select(player => player.PlayerId == playerId ? player with { IsEliminated = true } : player)
                    .ToArray(),
            },
            _ => gameState,
        };
        if (scenario == "missing_player")
        {
            playerId = new PlayerId("missing_player");
        }

        var result = PropertyUpgradeManager.BuyUpgrade(gameState, playerId, new TileId("property_01"));

        Assert.Equal(expectedKind, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Theory]
    [InlineData("utility_01", PropertyUpgradeResultKind.InvalidProperty)]
    [InlineData("missing_property", PropertyUpgradeResultKind.InvalidProperty)]
    [InlineData("property_03", PropertyUpgradeResultKind.PropertyNotOwned)]
    public void BuyUpgrade_RejectsInvalidPropertyRequests(string propertyTileId, PropertyUpgradeResultKind expectedKind)
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01", "property_02"),
            CreatePlayer("player_2", 1500));

        var result = PropertyUpgradeManager.BuyUpgrade(
            gameState,
            new PlayerId("player_1"),
            new TileId(propertyTileId));

        Assert.Equal(expectedKind, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void BuyUpgrade_RejectsIncompleteBuildableGroupOwnership()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01"),
            CreatePlayer("player_2", 1500, "property_02"));

        var result = PropertyUpgradeManager.BuyUpgrade(
            gameState,
            new PlayerId("player_1"),
            new TileId("property_01"));

        Assert.Equal(PropertyUpgradeResultKind.IncompleteGroupOwnership, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void BuyUpgrade_RejectsMortgagedGroupProperty()
    {
        var property02 = new TileId("property_02");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01", "property_02"),
            CreatePlayer("player_2", 1500)) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property02] = new(property02, new PropertyStateData(isMortgaged: true)),
            },
        };

        var result = PropertyUpgradeManager.BuyUpgrade(
            gameState,
            new PlayerId("player_1"),
            new TileId("property_01"));

        Assert.Equal(PropertyUpgradeResultKind.MortgagedGroupProperty, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void BuyUpgrade_RejectsUnevenUpgrade()
    {
        var property01 = new TileId("property_01");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01", "property_02"),
            CreatePlayer("player_2", 1500)) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(upgradeLevel: 1)),
            },
        };

        var result = PropertyUpgradeManager.BuyUpgrade(
            gameState,
            new PlayerId("player_1"),
            property01);

        Assert.Equal(PropertyUpgradeResultKind.UnevenUpgrade, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void BuyUpgrade_RejectsMaximumUpgradeLevel()
    {
        var property01 = new TileId("property_01");
        var property02 = new TileId("property_02");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01", "property_02"),
            CreatePlayer("player_2", 1500)) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(upgradeLevel: 5)),
                [property02] = new(property02, new PropertyStateData(upgradeLevel: 5)),
            },
        };

        var result = PropertyUpgradeManager.BuyUpgrade(
            gameState,
            new PlayerId("player_1"),
            property01);

        Assert.Equal(PropertyUpgradeResultKind.MaximumUpgradeLevel, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void BuyUpgrade_RejectsInsufficientCash()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", 49, "property_01", "property_02"),
            CreatePlayer("player_2", 1500));

        var result = PropertyUpgradeManager.BuyUpgrade(
            gameState,
            new PlayerId("player_1"),
            new TileId("property_01"));

        Assert.Equal(PropertyUpgradeResultKind.InsufficientCash, result.ResultKind);
        Assert.Equal(new Money(50), result.UpgradeCost);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void BuyUpgrade_RejectsUnavailableRentTier()
    {
        var property01 = new TileId("property_01");
        var property02 = new TileId("property_02");
        var board = DefaultBoardFactory.Create();
        var tiles = board.Tiles
            .Select(tile => tile.TileId == property01
                ? tile with { RentTable = new[] { new Money(2), new Money(10), new Money(30), new Money(90), new Money(160) } }
                : tile)
            .ToArray();
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01", "property_02"),
            CreatePlayer("player_2", 1500)) with
        {
            Board = board with { Tiles = tiles },
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(upgradeLevel: 4)),
                [property02] = new(property02, new PropertyStateData(upgradeLevel: 4)),
            },
        };

        var result = PropertyUpgradeManager.BuyUpgrade(gameState, new PlayerId("player_1"), property01);

        Assert.Equal(PropertyUpgradeResultKind.RentTierUnavailable, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void SellUpgrade_RefundsConfiguredPercentAndDecrementsLevel()
    {
        var property01 = new TileId("property_01");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01", "property_02"),
            CreatePlayer("player_2", 1500)) with
        {
            Rules = UpgradeRules(upgradeSellRefundPercent: 25),
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(upgradeLevel: 1)),
            },
        };

        var result = PropertyUpgradeManager.SellUpgrade(gameState, new PlayerId("player_1"), property01);

        Assert.True(result.UpgradeSold);
        Assert.Equal(new Money(12), result.RefundAmount);
        Assert.Equal(new Money(1512), result.Money);
        Assert.Equal(0, result.UpgradeLevel);
        Assert.DoesNotContain(property01, result.GameState.PropertyStates.Keys);
    }

    [Fact]
    public void SellUpgrade_RejectsMinimumUpgradeLevel()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01", "property_02"),
            CreatePlayer("player_2", 1500));

        var result = PropertyUpgradeManager.SellUpgrade(
            gameState,
            new PlayerId("player_1"),
            new TileId("property_01"));

        Assert.Equal(PropertyUpgradeResultKind.MinimumUpgradeLevel, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void SellUpgrade_RejectsUnevenDowngrade()
    {
        var property01 = new TileId("property_01");
        var property02 = new TileId("property_02");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01", "property_02"),
            CreatePlayer("player_2", 1500)) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(upgradeLevel: 1)),
                [property02] = new(property02, new PropertyStateData(upgradeLevel: 2)),
            },
        };

        var result = PropertyUpgradeManager.SellUpgrade(gameState, new PlayerId("player_1"), property01);

        Assert.Equal(PropertyUpgradeResultKind.UnevenDowngrade, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void SellUpgrade_PreservesDamageWhenDowngrading()
    {
        var property01 = new TileId("property_01");
        var property02 = new TileId("property_02");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 1500, "property_01", "property_02"),
            CreatePlayer("player_2", 1500)) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(25, upgradeLevel: 2)),
                [property02] = new(property02, new PropertyStateData(upgradeLevel: 2)),
            },
        };

        var result = PropertyUpgradeManager.SellUpgrade(gameState, new PlayerId("player_1"), property01);

        Assert.True(result.UpgradeSold);
        Assert.Equal(25, result.GameState.PropertyStates[property01].Data.DamagePercent);
        Assert.Equal(1, result.GameState.PropertyStates[property01].Data.UpgradeLevel);
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
            EndedAtUtc: null)
        {
            Rules = UpgradeRules(),
        };
    }

    private static GameRules UpgradeRules(int upgradeSellRefundPercent = 50)
    {
        return GameRulesPresets.MonoJoeyDefault with
        {
            Economy = GameRulesPresets.MonoJoeyDefault.Economy with
            {
                UpgradesEnabled = true,
                UpgradeSellRefundPercent = upgradeSellRefundPercent,
            },
        };
    }

    private static Player CreatePlayer(
        string playerId,
        int money,
        params string[] ownedPropertyIds)
    {
        return new Player(
            new PlayerId(playerId),
            playerId,
            $"token_{playerId}",
            $"color_{playerId}",
            new Money(money),
            new TileId("start"),
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
