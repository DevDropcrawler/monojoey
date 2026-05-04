namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class PropertyStateManagerTests
{
    [Fact]
    public void ApplyEarthquake_DamagesOnlyOwnedPurchasableProperties()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", "start", "property_01", "free_space_01"),
            CreatePlayer("player_2", "start"));

        var result = PropertyStateManager.ApplyEarthquake(
            gameState,
            new[] { "property_01", "property_02", "free_space_01", "missing_property" },
            damagePercent: 50);

        var propertyState = Assert.Single(result.PropertyStates);
        Assert.Equal(new TileId("property_01"), propertyState.Key);
        Assert.Equal(50, propertyState.Value.Data.DamagePercent);
        Assert.Empty(gameState.PropertyStates);
        Assert.Contains(new TileId("property_01"), result.Players[0].OwnedPropertyIds);
    }

    [Fact]
    public void ApplyEarthquake_DeduplicatesAndAppliesInDeterministicTileOrder()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", "start", "property_02", "property_01"));

        var result = PropertyStateManager.ApplyEarthquake(
            gameState,
            new[] { "property_02", "property_01", "property_02" },
            damagePercent: 25);

        Assert.Equal(
            new[] { "property_01", "property_02" },
            result.PropertyStates.Keys.Select(tileId => tileId.Value).ToArray());
        Assert.All(result.PropertyStates.Values, state => Assert.Equal(25, state.Data.DamagePercent));
    }

    [Fact]
    public void ApplyEarthquake_PreservesUnrelatedPropertyStates()
    {
        var property03 = new TileId("property_03");
        var existingStates = new Dictionary<TileId, PropertyState>
        {
            [property03] = new(property03, new PropertyStateData(30)),
        };
        var gameState = CreateGameState(
            CreatePlayer("player_1", "start", "property_01", "property_03")) with
        {
            PropertyStates = existingStates,
        };

        var result = PropertyStateManager.ApplyEarthquake(
            gameState,
            new[] { "property_01" },
            damagePercent: 40);

        Assert.Equal(40, result.PropertyStates[new TileId("property_01")].Data.DamagePercent);
        Assert.Equal(30, result.PropertyStates[property03].Data.DamagePercent);
    }

    [Fact]
    public void ApplyEarthquake_UsesMaxDamageAndDoesNotRepair()
    {
        var property01 = new TileId("property_01");
        var gameState = CreateGameState(
            CreatePlayer("player_1", "start", "property_01")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(60)),
            },
        };

        var lowerDamageResult = PropertyStateManager.ApplyEarthquake(
            gameState,
            new[] { "property_01" },
            damagePercent: 40);
        var zeroDamageResult = PropertyStateManager.ApplyEarthquake(
            lowerDamageResult,
            new[] { "property_01" },
            damagePercent: 0);
        var higherDamageResult = PropertyStateManager.ApplyEarthquake(
            zeroDamageResult,
            new[] { "property_01" },
            damagePercent: 80);

        Assert.Same(gameState, lowerDamageResult);
        Assert.Same(lowerDamageResult, zeroDamageResult);
        Assert.Equal(80, higherDamageResult.PropertyStates[property01].Data.DamagePercent);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ApplyEarthquake_RejectsDamageOutsideValidRange(int damagePercent)
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", "start", "property_01"));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PropertyStateManager.ApplyEarthquake(gameState, new[] { "property_01" }, damagePercent));
    }

    [Fact]
    public void RepairDamagedOwnedProperties_ReducesDamageAndDeductsFloorCost()
    {
        var property01 = new TileId("property_01");
        var gameState = CreateGameState(
            CreatePlayer("player_1", "start", "property_01")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(50)),
            },
        };

        var result = PropertyStateManager.RepairDamagedOwnedProperties(
            gameState,
            new PlayerId("player_1"));

        Assert.Equal(40, result.PropertyStates[property01].Data.DamagePercent);
        Assert.Equal(new Money(1494), result.Players[0].Money);
        Assert.Equal(50, gameState.PropertyStates[property01].Data.DamagePercent);
        Assert.Equal(new Money(1500), gameState.Players[0].Money);
    }

    [Fact]
    public void RepairDamagedOwnedProperties_RemovesFullyRepairedStateAndCapsCostAtRemainingDamage()
    {
        var property01 = new TileId("property_01");
        var gameState = CreateGameState(
            CreatePlayer("player_1", "start", "property_01")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(5)),
            },
        };

        var result = PropertyStateManager.RepairDamagedOwnedProperties(
            gameState,
            new PlayerId("player_1"));

        Assert.DoesNotContain(property01, result.PropertyStates.Keys);
        Assert.Equal(new Money(1497), result.Players[0].Money);
    }

    [Fact]
    public void RepairDamagedOwnedProperties_ProcessesOwnedDamagedPropertiesInOrdinalTileOrder()
    {
        var property01 = new TileId("property_01");
        var property02 = new TileId("property_02");
        var property03 = new TileId("property_03");
        var gameState = CreateGameState(
            CreatePlayer("player_1", "start", "property_03", "property_02", "property_01") with
            {
                Money = new Money(12),
            }) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property03] = new(property03, new PropertyStateData(20)),
                [property02] = new(property02, new PropertyStateData(20)),
                [property01] = new(property01, new PropertyStateData(20)),
            },
        };

        var result = PropertyStateManager.RepairDamagedOwnedProperties(
            gameState,
            new PlayerId("player_1"));

        Assert.Equal(new Money(0), result.Players[0].Money);
        Assert.Equal(10, result.PropertyStates[property01].Data.DamagePercent);
        Assert.Equal(10, result.PropertyStates[property02].Data.DamagePercent);
        Assert.Equal(20, result.PropertyStates[property03].Data.DamagePercent);
        Assert.False(result.Players[0].IsBankrupt);
        Assert.False(result.Players[0].IsEliminated);
    }

    [Fact]
    public void RepairDamagedOwnedProperties_StopsAtFirstUnaffordablePropertyAndPreservesLaterProperties()
    {
        var property01 = new TileId("property_01");
        var property02 = new TileId("property_02");
        var property03 = new TileId("property_03");
        var gameState = CreateGameState(
            CreatePlayer("player_1", "start", "property_01", "property_02", "property_03") with
            {
                Money = new Money(9),
            }) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(20)),
                [property02] = new(property02, new PropertyStateData(20)),
                [property03] = new(property03, new PropertyStateData(20)),
            },
        };

        var result = PropertyStateManager.RepairDamagedOwnedProperties(
            gameState,
            new PlayerId("player_1"));

        Assert.Equal(new Money(3), result.Players[0].Money);
        Assert.Equal(10, result.PropertyStates[property01].Data.DamagePercent);
        Assert.Equal(20, result.PropertyStates[property02].Data.DamagePercent);
        Assert.Equal(20, result.PropertyStates[property03].Data.DamagePercent);
        Assert.False(result.Players[0].IsBankrupt);
        Assert.False(result.Players[0].IsEliminated);
    }

    [Fact]
    public void RepairDamagedOwnedProperties_RepairsCurrentOwnerOnlyAndPreservesUndamagedEntries()
    {
        var property01 = new TileId("property_01");
        var property02 = new TileId("property_02");
        var property03 = new TileId("property_03");
        var gameState = CreateGameState(
            CreatePlayer("player_1", "start", "property_01", "property_03"),
            CreatePlayer("player_2", "start", "property_02")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(30)),
                [property02] = new(property02, new PropertyStateData(30)),
                [property03] = new(property03, new PropertyStateData()),
            },
        };

        var result = PropertyStateManager.RepairDamagedOwnedProperties(
            gameState,
            new PlayerId("player_1"));

        Assert.Equal(20, result.PropertyStates[property01].Data.DamagePercent);
        Assert.Equal(30, result.PropertyStates[property02].Data.DamagePercent);
        Assert.Equal(0, result.PropertyStates[property03].Data.DamagePercent);
        Assert.Equal(new Money(1494), result.Players[0].Money);
        Assert.Equal(new Money(1500), result.Players[1].Money);
    }

    [Fact]
    public void RepairDamagedOwnedProperties_WhenNoEligibleDamageDoesNotMutateState()
    {
        var property02 = new TileId("property_02");
        var gameState = CreateGameState(
            CreatePlayer("player_1", "start", "property_01"),
            CreatePlayer("player_2", "start", "property_02")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property02] = new(property02, new PropertyStateData(30)),
            },
        };

        var result = PropertyStateManager.RepairDamagedOwnedProperties(
            gameState,
            new PlayerId("player_1"));

        Assert.Same(gameState, result);
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
        params string[] ownedPropertyIds)
    {
        return new Player(
            new PlayerId(playerId),
            playerId,
            $"token_{playerId}",
            $"color_{playerId}",
            new Money(1500),
            new TileId(currentTileId),
            ownedPropertyIds.Select(propertyId => new TileId(propertyId)).ToHashSet(),
            new HashSet<CardId>(),
            IsBankrupt: false,
            IsEliminated: false);
    }
}
