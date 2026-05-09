namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class TradeManagerTests
{
    private static readonly PlayerId FirstPlayerId = new("player_1");
    private static readonly PlayerId SecondPlayerId = new("player_2");

    [Fact]
    public void SettleTrade_SettlesCashOnlyTrade()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 100), CreatePlayer("player_2", 50));

        var result = TradeManager.SettleTrade(
            gameState,
            FirstPlayerId,
            Assets(cash: 25),
            SecondPlayerId,
            Assets(cash: 10));

        Assert.True(result.TradeSettled);
        Assert.Equal(new Money(85), result.GameState.Players[0].Money);
        Assert.Equal(new Money(65), result.GameState.Players[1].Money);
        Assert.Equal(2, result.CashTransferResults.Count);
        Assert.Empty(result.OwnershipChanges);
        Assert.Equal(new Money(100), gameState.Players[0].Money);
    }

    [Fact]
    public void SettleTrade_SettlesPropertyForCashTrade()
    {
        var propertyTileId = new TileId("property_01");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100, "property_01"),
            CreatePlayer("player_2", 200));

        var result = TradeManager.SettleTrade(
            gameState,
            FirstPlayerId,
            Assets(properties: "property_01"),
            SecondPlayerId,
            Assets(cash: 60));

        Assert.True(result.TradeSettled);
        Assert.Equal(new Money(160), result.GameState.Players[0].Money);
        Assert.Equal(new Money(140), result.GameState.Players[1].Money);
        Assert.DoesNotContain(propertyTileId, result.GameState.Players[0].OwnedPropertyIds);
        Assert.Contains(propertyTileId, result.GameState.Players[1].OwnedPropertyIds);
        var change = Assert.Single(result.OwnershipChanges);
        Assert.Equal(propertyTileId, change.PropertyTileId);
        Assert.Equal(FirstPlayerId, change.PreviousOwnerId);
        Assert.Equal(SecondPlayerId, change.NewOwnerId);
    }

    [Fact]
    public void SettleTrade_SettlesPropertyForPropertySwapInTileIdOrder()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100, "property_03", "property_01"),
            CreatePlayer("player_2", 100, "transport_01"));

        var result = TradeManager.SettleTrade(
            gameState,
            FirstPlayerId,
            Assets(properties: ["property_03", "property_01"]),
            SecondPlayerId,
            Assets(properties: "transport_01"));

        Assert.True(result.TradeSettled);
        Assert.Contains(new TileId("transport_01"), result.GameState.Players[0].OwnedPropertyIds);
        Assert.Contains(new TileId("property_01"), result.GameState.Players[1].OwnedPropertyIds);
        Assert.Contains(new TileId("property_03"), result.GameState.Players[1].OwnedPropertyIds);
        Assert.Collection(
            result.OwnershipChanges,
            change => Assert.Equal(new TileId("property_01"), change.PropertyTileId),
            change => Assert.Equal(new TileId("property_03"), change.PropertyTileId),
            change => Assert.Equal(new TileId("transport_01"), change.PropertyTileId));
    }

    [Fact]
    public void SettleTrade_AllowsOneSidedCashGift()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 100), CreatePlayer("player_2", 50));

        var result = TradeManager.SettleTrade(
            gameState,
            FirstPlayerId,
            Assets(cash: 30),
            SecondPlayerId,
            Assets());

        Assert.True(result.TradeSettled);
        Assert.Equal(new Money(70), result.GameState.Players[0].Money);
        Assert.Equal(new Money(80), result.GameState.Players[1].Money);
    }

    [Fact]
    public void SettleTrade_AllowsOneSidedPropertyGiftAndPreservesPropertyStates()
    {
        var propertyTileId = new TileId("property_03");
        var propertyStates = new Dictionary<TileId, PropertyState>
        {
            [propertyTileId] = new(propertyTileId, new PropertyStateData(35, isMortgaged: true)),
        };
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100, "property_03"),
            CreatePlayer("player_2", 50)) with
        {
            PropertyStates = propertyStates,
        };

        var result = TradeManager.SettleTrade(
            gameState,
            FirstPlayerId,
            Assets(properties: "property_03"),
            SecondPlayerId,
            Assets());

        Assert.True(result.TradeSettled);
        Assert.Contains(propertyTileId, result.GameState.Players[1].OwnedPropertyIds);
        Assert.Same(propertyStates, result.GameState.PropertyStates);
        Assert.Equal(35, result.GameState.PropertyStates[propertyTileId].Data.DamagePercent);
        Assert.True(result.GameState.PropertyStates[propertyTileId].Data.IsMortgaged);
    }

    [Theory]
    [InlineData("auction", TradeSettlementResultKind.ActiveAuction)]
    [InlineData("unresolved_tile", TradeSettlementResultKind.UnresolvedTileExecution)]
    [InlineData("completed", TradeSettlementResultKind.GameNotInProgress)]
    public void SettleTrade_RejectsBlockedGameStates(string scenario, TradeSettlementResultKind expectedKind)
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 100), CreatePlayer("player_2", 50));
        gameState = scenario switch
        {
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

        var result = TradeManager.SettleTrade(
            gameState,
            FirstPlayerId,
            Assets(cash: 1),
            SecondPlayerId,
            Assets());

        Assert.Equal(expectedKind, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Theory]
    [InlineData("missing", TradeSettlementResultKind.PlayerNotInGame)]
    [InlineData("same", TradeSettlementResultKind.SamePlayer)]
    [InlineData("bankrupt", TradeSettlementResultKind.PlayerBankrupt)]
    [InlineData("eliminated", TradeSettlementResultKind.PlayerEliminated)]
    public void SettleTrade_RejectsInvalidPlayers(string scenario, TradeSettlementResultKind expectedKind)
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 100), CreatePlayer("player_2", 50));
        var secondPlayerId = SecondPlayerId;
        gameState = scenario switch
        {
            "bankrupt" => CreateGameState(
                CreatePlayer("player_1", 100, isBankrupt: true),
                CreatePlayer("player_2", 50)),
            "eliminated" => CreateGameState(
                CreatePlayer("player_1", 100, isEliminated: true),
                CreatePlayer("player_2", 50)),
            _ => gameState,
        };

        if (scenario == "missing")
        {
            secondPlayerId = new PlayerId("missing_player");
        }
        else if (scenario == "same")
        {
            secondPlayerId = FirstPlayerId;
        }

        var result = TradeManager.SettleTrade(
            gameState,
            FirstPlayerId,
            Assets(cash: 1),
            secondPlayerId,
            Assets());

        Assert.Equal(expectedKind, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Theory]
    [InlineData("negative_cash", TradeSettlementResultKind.NegativeCash)]
    [InlineData("empty", TradeSettlementResultKind.EmptyTrade)]
    [InlineData("duplicate_property", TradeSettlementResultKind.DuplicateProperty)]
    [InlineData("overlap", TradeSettlementResultKind.PropertyOfferedByBothSides)]
    [InlineData("invalid_property", TradeSettlementResultKind.InvalidProperty)]
    [InlineData("wrong_owner", TradeSettlementResultKind.PropertyNotOwnedByOfferingPlayer)]
    [InlineData("duplicate_existing_ownership", TradeSettlementResultKind.PropertyOwnedByMultiplePlayers)]
    [InlineData("insufficient_cash", TradeSettlementResultKind.InsufficientCash)]
    [InlineData("overflow", TradeSettlementResultKind.UnsafeMoneyBalance)]
    public void SettleTrade_RejectsInvalidTradeAssets(string scenario, TradeSettlementResultKind expectedKind)
    {
        var gameState = scenario switch
        {
            "duplicate_existing_ownership" => CreateGameState(
                CreatePlayer("player_1", 100, "property_01"),
                CreatePlayer("player_2", 50, "property_01")),
            "overflow" => CreateGameState(CreatePlayer("player_1", int.MaxValue), CreatePlayer("player_2", 10)),
            _ => CreateGameState(
                CreatePlayer("player_1", 100, "property_01"),
                CreatePlayer("player_2", 50, "property_02")),
        };
        var firstAssets = Assets(cash: 1);
        var secondAssets = Assets();

        switch (scenario)
        {
            case "negative_cash":
                firstAssets = Assets(cash: -1);
                break;
            case "empty":
                firstAssets = Assets();
                break;
            case "duplicate_property":
                firstAssets = Assets(properties: ["property_01", "property_01"]);
                break;
            case "overlap":
                firstAssets = Assets(properties: "property_01");
                secondAssets = Assets(properties: "property_01");
                break;
            case "invalid_property":
                firstAssets = Assets(properties: "free_space_01");
                break;
            case "wrong_owner":
                firstAssets = Assets(properties: "property_02");
                break;
            case "duplicate_existing_ownership":
                firstAssets = Assets(properties: "property_01");
                break;
            case "insufficient_cash":
                firstAssets = Assets(cash: 101);
                break;
            case "overflow":
                firstAssets = Assets();
                secondAssets = Assets(cash: 1);
                break;
        }

        var result = TradeManager.SettleTrade(
            gameState,
            FirstPlayerId,
            firstAssets,
            SecondPlayerId,
            secondAssets);

        Assert.Equal(expectedKind, result.ResultKind);
        Assert.Same(gameState, result.GameState);
        Assert.Empty(result.CashTransferResults);
        Assert.Empty(result.OwnershipChanges);
    }

    [Fact]
    public void SettleTrade_PreservesUnrelatedState()
    {
        var currentPlayerId = new PlayerId("player_3");
        var auctionState = (AuctionState?)null;
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100),
            CreatePlayer("player_2", 50),
            CreatePlayer("player_3", 75)) with
        {
            CurrentTurnPlayerId = currentPlayerId,
            TurnNumber = 7,
            ActiveAuctionState = auctionState,
            HasRolledThisTurn = true,
            HasResolvedTileThisTurn = false,
            HasExecutedTileThisTurn = false,
        };

        var result = TradeManager.SettleTrade(
            gameState,
            FirstPlayerId,
            Assets(cash: 5),
            SecondPlayerId,
            Assets());

        Assert.True(result.TradeSettled);
        Assert.Equal(currentPlayerId, result.GameState.CurrentTurnPlayerId);
        Assert.Equal(7, result.GameState.TurnNumber);
        Assert.True(result.GameState.HasRolledThisTurn);
        Assert.False(result.GameState.HasResolvedTileThisTurn);
        Assert.False(result.GameState.HasExecutedTileThisTurn);
        Assert.Same(gameState.Board, result.GameState.Board);
        Assert.Equal(new Money(75), result.GameState.Players[2].Money);
    }

    private static TradeAssets Assets(int cash = 0, params string[] properties)
    {
        return new TradeAssets(new Money(cash), properties.Select(property => new TileId(property)).ToArray());
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
        int money,
        params string[] ownedPropertyIds)
    {
        return CreatePlayer(
            playerId,
            money,
            isBankrupt: false,
            isEliminated: false,
            ownedPropertyIds);
    }

    private static Player CreatePlayer(
        string playerId,
        int money,
        bool isBankrupt = false,
        bool isEliminated = false,
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
            isBankrupt,
            isEliminated);
    }

    private static AuctionState CreateAuctionState()
    {
        return new AuctionState(
            new TileId("property_01"),
            FirstPlayerId,
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
