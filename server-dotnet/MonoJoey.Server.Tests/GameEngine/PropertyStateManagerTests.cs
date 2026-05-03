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
