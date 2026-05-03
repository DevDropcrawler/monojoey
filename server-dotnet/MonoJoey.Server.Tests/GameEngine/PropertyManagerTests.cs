namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class PropertyManagerTests
{
    [Fact]
    public void AssignOwner_AssignsUnownedPropertyToOwner()
    {
        var ownerId = new PlayerId("player_1");
        var propertyTileId = new TileId("property_01");
        var gameState = CreateGameState(CreatePlayer("player_1", "start"));

        var result = PropertyManager.AssignOwner(gameState, propertyTileId, ownerId);

        Assert.Contains(propertyTileId, result.Players[0].OwnedPropertyIds);
        Assert.Empty(gameState.Players[0].OwnedPropertyIds);
    }

    [Fact]
    public void AssignOwner_IgnoresAndPreservesPropertyState()
    {
        var ownerId = new PlayerId("player_1");
        var propertyTileId = new TileId("property_01");
        var propertyStates = new Dictionary<TileId, PropertyState>
        {
            [propertyTileId] = new(propertyTileId, new PropertyStateData()),
        };
        var gameState = CreateGameState(CreatePlayer("player_1", "start")) with
        {
            PropertyStates = propertyStates,
        };

        var result = PropertyManager.AssignOwner(gameState, propertyTileId, ownerId);

        Assert.Contains(propertyTileId, result.Players[0].OwnedPropertyIds);
        Assert.Same(propertyStates, result.PropertyStates);
    }

    [Fact]
    public void BuyProperty_AssignsUnownedPropertyToBuyerAndPaysPrice()
    {
        var buyerId = new PlayerId("player_1");
        var propertyTileId = new TileId("property_01");
        var gameState = CreateGameState(CreatePlayer("player_1", "property_01"));

        var result = PropertyManager.BuyProperty(gameState, buyerId, propertyTileId);

        Assert.Equal(buyerId, result.BuyerId);
        Assert.Equal(propertyTileId, result.PropertyTileId);
        Assert.Equal(new Money(60), result.PricePaid);
        Assert.Equal(new Money(1440), result.GameState.Players[0].Money);
        Assert.Contains(propertyTileId, result.GameState.Players[0].OwnedPropertyIds);
    }

    [Fact]
    public void PayRentForCurrentTile_ChargesRentToLandingPlayer()
    {
        var propertyTileId = new TileId("property_01");
        var gameState = CreateGameState(
            CreatePlayer("player_1", "property_01"),
            CreatePlayer("player_2", "start", 1500, "property_01"));

        var result = PropertyManager.PayRentForCurrentTile(gameState, new PlayerId("player_1"));

        Assert.True(result.RentCharged);
        Assert.Equal(propertyTileId, result.TileId);
        Assert.Equal(new Money(2), result.RentDue);
        Assert.Equal(new Money(2), result.RentPaid);
        Assert.Equal(new Money(1498), result.GameState.Players[0].Money);
    }

    [Fact]
    public void PayRentForCurrentTile_IgnoresAndPreservesPropertyState()
    {
        var propertyTileId = new TileId("property_01");
        var propertyStates = new Dictionary<TileId, PropertyState>
        {
            [propertyTileId] = new(propertyTileId, new PropertyStateData()),
        };
        var gameState = CreateGameState(
            CreatePlayer("player_1", "property_01"),
            CreatePlayer("player_2", "start", 1500, "property_01")) with
        {
            PropertyStates = propertyStates,
        };

        var result = PropertyManager.PayRentForCurrentTile(gameState, new PlayerId("player_1"));

        Assert.True(result.RentCharged);
        Assert.Equal(new Money(2), result.RentDue);
        Assert.Equal(new Money(1498), result.GameState.Players[0].Money);
        Assert.Equal(new Money(1502), result.GameState.Players[1].Money);
        Assert.Same(propertyStates, result.GameState.PropertyStates);
    }

    [Fact]
    public void PayRentForCurrentTile_TransfersRentToOwner()
    {
        var ownerId = new PlayerId("player_2");
        var gameState = CreateGameState(
            CreatePlayer("player_1", "property_01"),
            CreatePlayer("player_2", "start", 1500, "property_01"));

        var result = PropertyManager.PayRentForCurrentTile(gameState, new PlayerId("player_1"));

        Assert.Equal(ownerId, result.OwnerId);
        Assert.Equal(new Money(1502), result.GameState.Players[1].Money);
    }

    [Fact]
    public void PayRentForCurrentTile_DoesNotChargeRentWhenLandingOnOwnProperty()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "property_01", 1500, "property_01"));

        var result = PropertyManager.PayRentForCurrentTile(gameState, new PlayerId("player_1"));

        Assert.False(result.RentCharged);
        Assert.Equal(new PlayerId("player_1"), result.OwnerId);
        Assert.Equal(new Money(1500), result.GameState.Players[0].Money);
    }

    [Fact]
    public void PayRentForCurrentTile_DoesNotChargeRentOnNonPropertyTile()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", "free_space_01"),
            CreatePlayer("player_2", "start", 1500, "property_01"));

        var result = PropertyManager.PayRentForCurrentTile(gameState, new PlayerId("player_1"));

        Assert.False(result.RentCharged);
        Assert.Null(result.OwnerId);
        Assert.Equal(new Money(1500), result.GameState.Players[0].Money);
        Assert.Equal(new Money(1500), result.GameState.Players[1].Money);
    }

    [Fact]
    public void PayRentForCurrentTile_DoesNotChargeRentOnUnownedProperty()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "property_01"));

        var result = PropertyManager.PayRentForCurrentTile(gameState, new PlayerId("player_1"));

        Assert.False(result.RentCharged);
        Assert.Null(result.OwnerId);
        Assert.Equal(new Money(1500), result.GameState.Players[0].Money);
    }

    [Fact]
    public void AssignOwner_RejectsUnknownOwner()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "start"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => PropertyManager.AssignOwner(gameState, new TileId("property_01"), new PlayerId("missing_player")));

        Assert.Equal("Property owner must exist in the game player list.", exception.Message);
    }

    [Fact]
    public void PayRentForCurrentTile_RejectsUnknownLandingPlayer()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "start"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => PropertyManager.PayRentForCurrentTile(gameState, new PlayerId("missing_player")));

        Assert.Equal("Landing player must exist in the game player list before rent can be paid.", exception.Message);
    }

    [Fact]
    public void AssignOwner_RejectsUnknownProperty()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "start"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => PropertyManager.AssignOwner(gameState, new TileId("missing_property"), new PlayerId("player_1")));

        Assert.Equal("Property tile must exist on the board.", exception.Message);
    }

    [Fact]
    public void AssignOwner_RejectsNonPropertyTile()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "start"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => PropertyManager.AssignOwner(gameState, new TileId("free_space_01"), new PlayerId("player_1")));

        Assert.Equal("Tile must be purchasable before it can be owned.", exception.Message);
    }

    [Fact]
    public void BuyProperty_RejectsAlreadyOwnedProperty()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", "property_01"),
            CreatePlayer("player_2", "start", 1500, "property_01"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => PropertyManager.BuyProperty(gameState, new PlayerId("player_1"), new TileId("property_01")));

        Assert.Equal("Property must be unowned before ownership can be assigned.", exception.Message);
    }

    [Fact]
    public void PayRentForCurrentTile_EliminatesLandingPlayerWhenRentCannotBePaid()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", "property_01", money: 1),
            CreatePlayer("player_2", "start", 1500, "property_01"));

        var result = PropertyManager.PayRentForCurrentTile(gameState, new PlayerId("player_1"));

        Assert.False(result.RentCharged);
        Assert.True(result.PlayerEliminated);
        Assert.Equal(new Money(2), result.RentDue);
        Assert.Equal(new Money(0), result.RentPaid);
        Assert.True(result.GameState.Players[0].IsBankrupt);
        Assert.True(result.GameState.Players[0].IsEliminated);
        Assert.Equal(new Money(1), result.GameState.Players[0].Money);
        Assert.Equal(new Money(1500), result.GameState.Players[1].Money);
        Assert.Equal(EliminationReason.CannotFulfillPayment, result.EliminationResult?.Reason);
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
