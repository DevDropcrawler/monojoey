namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class TurnManagerTests
{
    [Fact]
    public void StartFirstTurn_SelectsFirstPlayerAndAwaitingRollPhase()
    {
        var gameState = CreateGameState("player_1", "player_2") with
        {
            HasRolledThisTurn = true,
            HasResolvedTileThisTurn = true,
            HasExecutedTileThisTurn = true,
            ActiveAuctionState = CreateAuctionState(),
        };

        var started = TurnManager.StartFirstTurn(gameState);

        Assert.Equal("player_1", started.CurrentTurnPlayerId?.Value);
        Assert.Equal(GamePhase.AwaitingRoll, started.Phase);
        Assert.Equal(1, started.TurnNumber);
        Assert.False(started.HasRolledThisTurn);
        Assert.False(started.HasResolvedTileThisTurn);
        Assert.False(started.HasExecutedTileThisTurn);
        Assert.Null(started.ActiveAuctionState);
    }

    [Fact]
    public void StartFirstTurn_SkipsEliminatedPlayers()
    {
        var gameState = CreateGameState("player_1", "player_2") with
        {
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("start"), isEliminated: true),
                CreatePlayer("player_2", new TileId("start")),
            },
        };

        var started = TurnManager.StartFirstTurn(gameState);

        Assert.Equal("player_2", started.CurrentTurnPlayerId?.Value);
    }

    [Fact]
    public void StartFirstTurn_SkipsLockedPlayers()
    {
        var gameState = CreateGameState("player_1", "player_2") with
        {
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("lockup_01"), isLockedUp: true),
                CreatePlayer("player_2", new TileId("start")),
            },
        };

        var started = TurnManager.StartFirstTurn(gameState);

        Assert.Equal("player_2", started.CurrentTurnPlayerId?.Value);
    }

    [Fact]
    public void StartFirstTurn_WhenAllActivePlayersAreLockedSelectsLockedActivePlayer()
    {
        var gameState = CreateGameState("player_1", "player_2") with
        {
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("lockup_01"), isLockedUp: true),
                CreatePlayer("player_2", new TileId("lockup_01"), isLockedUp: true),
            },
        };

        var started = TurnManager.StartFirstTurn(gameState);

        Assert.Equal("player_1", started.CurrentTurnPlayerId?.Value);
        Assert.True(started.Players[0].IsLockedUp);
    }

    [Fact]
    public void StartFirstTurn_ChargesLoanInterestBeforePlayerCanRoll()
    {
        var gameState = CreateGameState("player_1", "player_2") with
        {
            Players = new[]
            {
                CreatePlayer(
                    "player_1",
                    new TileId("start"),
                    loanState: new PlayerLoanState(new Money(200), 30, Money.Zero, LoanTier: 2)),
                CreatePlayer("player_2", new TileId("start")),
            },
        };

        var started = TurnManager.StartFirstTurn(gameState);

        Assert.Equal("player_1", started.CurrentTurnPlayerId?.Value);
        Assert.Equal(GamePhase.AwaitingRoll, started.Phase);
        Assert.Equal(new Money(1440), started.Players[0].Money);
    }

    [Fact]
    public void StartFirstTurn_RepairsSelectedPlayersPropertiesAfterLoanInterestBeforeRoll()
    {
        var property01 = new TileId("property_01");
        var gameState = CreateGameState("player_1", "player_2") with
        {
            Players = new[]
            {
                CreatePlayer(
                    "player_1",
                    new TileId("start"),
                    loanState: new PlayerLoanState(new Money(100), 20, Money.Zero, LoanTier: 1),
                    ownedPropertyIds: new[] { "property_01" }),
                CreatePlayer(
                    "player_2",
                    new TileId("start"),
                    ownedPropertyIds: new[] { "property_02" }),
            },
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(20)),
                [new TileId("property_02")] = new(new TileId("property_02"), new PropertyStateData(20)),
            },
        };

        var started = TurnManager.StartFirstTurn(gameState);

        Assert.Equal("player_1", started.CurrentTurnPlayerId?.Value);
        Assert.Equal(GamePhase.AwaitingRoll, started.Phase);
        Assert.False(started.HasRolledThisTurn);
        Assert.Equal(new Money(1474), started.Players[0].Money);
        Assert.Equal(10, started.PropertyStates[property01].Data.DamagePercent);
        Assert.Equal(20, started.PropertyStates[new TileId("property_02")].Data.DamagePercent);
    }

    [Fact]
    public void StartFirstTurn_IgnoresSchemaRateFieldsWhenDerivingLoanConfig()
    {
        var gameState = CreateGameState("player_1", "player_2") with
        {
            Rules = GameRulesPresets.MonoJoeyDefault with
            {
                Loans = GameRulesPresets.MonoJoeyDefault.Loans with
                {
                    BaseInterestRate = 0.99m,
                    InterestRateIncreasePerLoan = 0.99m,
                    InterestRateIncreasePerDebtTier = 0.99m,
                    MinimumInterestPayment = 999,
                },
            },
            Players = new[]
            {
                CreatePlayer(
                    "player_1",
                    new TileId("start"),
                    loanState: new PlayerLoanState(new Money(200), 30, Money.Zero, LoanTier: 2)),
                CreatePlayer("player_2", new TileId("start")),
            },
        };

        var started = TurnManager.StartFirstTurn(gameState);

        Assert.Equal(new Money(1440), started.Players[0].Money);
        Assert.Equal(new Money(60), started.Players[0].LoanState?.NextTurnInterestDue);
    }

    [Fact]
    public void StartFirstTurn_EliminatesPlayerWhoCannotPayLoanInterestBeforeRoll()
    {
        var gameState = CreateGameState("player_1", "player_2") with
        {
            Players = new[]
            {
                CreatePlayer(
                    "player_1",
                    new TileId("start"),
                    money: 5,
                    loanState: new PlayerLoanState(new Money(100), 20, Money.Zero, LoanTier: 1)),
                CreatePlayer("player_2", new TileId("start")),
            },
        };

        var started = TurnManager.StartFirstTurn(gameState);

        Assert.Equal("player_1", started.CurrentTurnPlayerId?.Value);
        Assert.Equal(new Money(-15), started.Players[0].Money);
        Assert.True(started.Players[0].IsBankrupt);
        Assert.True(started.Players[0].IsEliminated);
        Assert.Throws<InvalidOperationException>(() => TurnManager.GetCurrentPlayer(started));
    }

    [Fact]
    public void AdvanceToNextTurn_SelectsNextPlayerInOrder()
    {
        var gameState = TurnManager.StartFirstTurn(CreateGameState("player_1", "player_2", "player_3")) with
        {
            HasRolledThisTurn = true,
            HasResolvedTileThisTurn = true,
            HasExecutedTileThisTurn = true,
            ActiveAuctionState = CreateAuctionState(),
        };

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_2", next.CurrentTurnPlayerId?.Value);
        Assert.Equal(2, next.TurnNumber);
        Assert.Equal(GamePhase.AwaitingRoll, next.Phase);
        Assert.False(next.HasRolledThisTurn);
        Assert.False(next.HasResolvedTileThisTurn);
        Assert.False(next.HasExecutedTileThisTurn);
        Assert.Null(next.ActiveAuctionState);
    }

    [Fact]
    public void AdvanceToNextTurn_ChargesNextPlayersLoanInterestBeforeRoll()
    {
        var gameState = CreateGameState("player_1", "player_2") with
        {
            CurrentTurnPlayerId = new PlayerId("player_1"),
            TurnNumber = 1,
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("start")),
                CreatePlayer(
                    "player_2",
                    new TileId("start"),
                    loanState: new PlayerLoanState(new Money(150), 20, Money.Zero, LoanTier: 1)),
            },
        };

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_2", next.CurrentTurnPlayerId?.Value);
        Assert.Equal(2, next.TurnNumber);
        Assert.Equal(GamePhase.AwaitingRoll, next.Phase);
        Assert.Equal(new Money(1470), next.Players[1].Money);
    }

    [Fact]
    public void AdvanceToNextTurn_RepairsNextPlayersPropertiesAfterLoanInterest()
    {
        var property02 = new TileId("property_02");
        var gameState = CreateGameState("player_1", "player_2") with
        {
            CurrentTurnPlayerId = new PlayerId("player_1"),
            TurnNumber = 1,
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("start"), ownedPropertyIds: new[] { "property_01" }),
                CreatePlayer(
                    "player_2",
                    new TileId("start"),
                    loanState: new PlayerLoanState(new Money(100), 20, Money.Zero, LoanTier: 1),
                    ownedPropertyIds: new[] { "property_02" }),
            },
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [new TileId("property_01")] = new(new TileId("property_01"), new PropertyStateData(20)),
                [property02] = new(property02, new PropertyStateData(20)),
            },
        };

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_2", next.CurrentTurnPlayerId?.Value);
        Assert.Equal(2, next.TurnNumber);
        Assert.Equal(new Money(1474), next.Players[1].Money);
        Assert.Equal(20, next.PropertyStates[new TileId("property_01")].Data.DamagePercent);
        Assert.Equal(10, next.PropertyStates[property02].Data.DamagePercent);
    }

    [Fact]
    public void AdvanceToNextTurn_SkipsPropertyRepairWhenLoanInterestEliminatesPlayer()
    {
        var property02 = new TileId("property_02");
        var gameState = CreateGameState("player_1", "player_2") with
        {
            CurrentTurnPlayerId = new PlayerId("player_1"),
            TurnNumber = 1,
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("start")),
                CreatePlayer(
                    "player_2",
                    new TileId("start"),
                    money: 5,
                    loanState: new PlayerLoanState(new Money(100), 20, Money.Zero, LoanTier: 1),
                    ownedPropertyIds: new[] { "property_02" }),
            },
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property02] = new(property02, new PropertyStateData(20)),
            },
        };

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_2", next.CurrentTurnPlayerId?.Value);
        Assert.Equal(new Money(-15), next.Players[1].Money);
        Assert.True(next.Players[1].IsEliminated);
        Assert.Equal(20, next.PropertyStates[property02].Data.DamagePercent);
    }

    [Fact]
    public void AdvanceToNextTurn_WrapsToFirstPlayerAfterLastPlayer()
    {
        var gameState = CreateGameState("player_1", "player_2") with
        {
            CurrentTurnPlayerId = new PlayerId("player_2"),
            TurnNumber = 2,
        };

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_1", next.CurrentTurnPlayerId?.Value);
        Assert.Equal(3, next.TurnNumber);
    }

    [Fact]
    public void AdvanceToNextTurn_SkipsEliminatedPlayer()
    {
        var gameState = CreateGameState("player_1", "player_2", "player_3") with
        {
            CurrentTurnPlayerId = new PlayerId("player_1"),
            TurnNumber = 1,
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("start")),
                CreatePlayer("player_2", new TileId("start"), isEliminated: true),
                CreatePlayer("player_3", new TileId("start")),
            },
        };

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_3", next.CurrentTurnPlayerId?.Value);
        Assert.Equal(2, next.TurnNumber);
    }

    [Fact]
    public void AdvanceToNextTurn_SkipsLockedPlayer()
    {
        var gameState = CreateGameState("player_1", "player_2", "player_3") with
        {
            CurrentTurnPlayerId = new PlayerId("player_1"),
            TurnNumber = 1,
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("start")),
                CreatePlayer("player_2", new TileId("lockup_01"), isLockedUp: true),
                CreatePlayer("player_3", new TileId("start")),
            },
        };

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_3", next.CurrentTurnPlayerId?.Value);
        Assert.Equal(2, next.TurnNumber);
    }

    [Fact]
    public void AdvanceToNextTurn_IgnoresNoOpStatusEffectsForEligibility()
    {
        var gameState = CreateGameState("player_1", "player_2", "player_3") with
        {
            CurrentTurnPlayerId = new PlayerId("player_1"),
            TurnNumber = 1,
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("start")),
                CreatePlayer("player_2", new TileId("start")) with
                {
                    StatusEffects = new[]
                    {
                        new PlayerStatusEffect(
                            "status_instance_1",
                            PlayerStatusEffectKind.NoOp,
                            new PlayerStatusEffectData("status_noop_foundation")),
                    },
                },
                CreatePlayer("player_3", new TileId("lockup_01"), isLockedUp: true),
            },
        };

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_2", next.CurrentTurnPlayerId?.Value);
        Assert.Single(next.Players[1].StatusEffects);
        Assert.True(next.Players[2].IsLockedUp);
    }

    [Fact]
    public void AdvanceToNextTurn_WhenAllActivePlayersAreLockedSelectsLockedActivePlayerWithoutUnlocking()
    {
        var gameState = CreateGameState("player_1", "player_2", "player_3") with
        {
            CurrentTurnPlayerId = new PlayerId("player_1"),
            TurnNumber = 1,
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("lockup_01"), isLockedUp: true),
                CreatePlayer("player_2", new TileId("lockup_01"), isLockedUp: true),
                CreatePlayer("player_3", new TileId("start"), isEliminated: true),
            },
        };

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_2", next.CurrentTurnPlayerId?.Value);
        Assert.True(next.Players[1].IsLockedUp);
        Assert.Equal(2, next.TurnNumber);
    }

    [Fact]
    public void AdvanceToNextTurn_SkipsMultipleEliminatedPlayers()
    {
        var gameState = CreateGameState("player_1", "player_2", "player_3", "player_4") with
        {
            CurrentTurnPlayerId = new PlayerId("player_1"),
            TurnNumber = 1,
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("start")),
                CreatePlayer("player_2", new TileId("start"), isEliminated: true),
                CreatePlayer("player_3", new TileId("start"), isEliminated: true),
                CreatePlayer("player_4", new TileId("start")),
            },
        };

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_4", next.CurrentTurnPlayerId?.Value);
        Assert.Equal(2, next.TurnNumber);
    }

    [Fact]
    public void AdvanceToNextTurn_GameContinuesWithRemainingPlayers()
    {
        var gameState = CreateGameState("player_1", "player_2", "player_3") with
        {
            CurrentTurnPlayerId = new PlayerId("player_3"),
            TurnNumber = 4,
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("start")),
                CreatePlayer("player_2", new TileId("start"), isEliminated: true),
                CreatePlayer("player_3", new TileId("start")),
            },
        };

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_1", next.CurrentTurnPlayerId?.Value);
        Assert.Equal(GamePhase.AwaitingRoll, next.Phase);
        Assert.Equal(5, next.TurnNumber);
    }

    [Fact]
    public void AdvanceToNextTurn_LastRemainingPlayerAdvancesToSelf()
    {
        var gameState = CreateGameState("player_1", "player_2", "player_3") with
        {
            CurrentTurnPlayerId = new PlayerId("player_1"),
            TurnNumber = 7,
            Players = new[]
            {
                CreatePlayer("player_1", new TileId("start")),
                CreatePlayer("player_2", new TileId("start"), isEliminated: true),
                CreatePlayer("player_3", new TileId("start"), isEliminated: true),
            },
        };

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_1", next.CurrentTurnPlayerId?.Value);
        Assert.Equal(8, next.TurnNumber);
    }

    [Fact]
    public void GetCurrentPlayer_ReturnsCurrentPlayer()
    {
        var gameState = TurnManager.StartFirstTurn(CreateGameState("player_1", "player_2"));

        var currentPlayer = TurnManager.GetCurrentPlayer(gameState);

        Assert.Equal("player_1", currentPlayer.PlayerId.Value);
    }

    [Fact]
    public void GetCurrentPlayer_RejectsEliminatedCurrentPlayer()
    {
        var gameState = CreateGameState("player_1") with
        {
            CurrentTurnPlayerId = new PlayerId("player_1"),
            Players = new[] { CreatePlayer("player_1", new TileId("start"), isEliminated: true) },
        };

        var exception = Assert.Throws<InvalidOperationException>(() => TurnManager.GetCurrentPlayer(gameState));

        Assert.Equal("Eliminated players cannot take turns.", exception.Message);
    }

    [Fact]
    public void GetCurrentPlayer_ReturnsLockedCurrentPlayer()
    {
        var gameState = CreateGameState("player_1") with
        {
            CurrentTurnPlayerId = new PlayerId("player_1"),
            Players = new[] { CreatePlayer("player_1", new TileId("lockup_01"), isLockedUp: true) },
        };

        var currentPlayer = TurnManager.GetCurrentPlayer(gameState);

        Assert.Equal("player_1", currentPlayer.PlayerId.Value);
        Assert.True(currentPlayer.IsLockedUp);
    }

    private static GameState CreateGameState(params string[] playerIds)
    {
        var startTileId = new TileId("start");

        return new GameState(
            new MatchId("match_123"),
            GamePhase.Lobby,
            DefaultBoardFactory.Create(),
            playerIds.Select(playerId => CreatePlayer(playerId, startTileId)).ToArray(),
            CurrentTurnPlayerId: null,
            TurnNumber: 0,
            DateTimeOffset.Parse("2026-04-26T00:00:00+00:00"),
            EndedAtUtc: null);
    }

    private static Player CreatePlayer(
        string playerId,
        TileId startTileId,
        bool isEliminated = false,
        int money = 1500,
        PlayerLoanState? loanState = null,
        bool isLockedUp = false,
        IEnumerable<string>? ownedPropertyIds = null)
    {
        return new Player(
            new PlayerId(playerId),
            playerId,
            $"token_{playerId}",
            $"color_{playerId}",
            new Money(money),
            startTileId,
            ownedPropertyIds?.Select(propertyId => new TileId(propertyId)).ToHashSet() ?? new HashSet<TileId>(),
            new HashSet<CardId>(),
            IsBankrupt: isEliminated,
            IsEliminated: isEliminated)
        {
            LoanState = loanState,
            IsLockedUp = isLockedUp,
        };
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
