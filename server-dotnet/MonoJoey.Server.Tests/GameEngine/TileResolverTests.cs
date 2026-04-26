namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class TileResolverTests
{
    [Fact]
    public void ResolveCurrentTile_ReturnsNoActionForFreeSpace()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "free_space_01"));

        var result = TileResolver.ResolveCurrentTile(gameState, new PlayerId("player_1"));

        Assert.Equal("player_1", result.PlayerId.Value);
        Assert.Equal("free_space_01", result.TileId.Value);
        Assert.Equal(6, result.TileIndex);
        Assert.Equal(TileType.FreeSpace, result.TileType);
        Assert.False(result.RequiresAction);
        Assert.True(result.NoAction);
        Assert.Equal(TileResolutionActionKind.NoAction, result.ActionKind);
    }

    [Fact]
    public void ResolveCurrentTile_ReturnsPropertyPlaceholderForProperty()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "property_01"));

        var result = TileResolver.ResolveCurrentTile(gameState, new PlayerId("player_1"));

        Assert.Equal("property_01", result.TileId.Value);
        Assert.Equal(1, result.TileIndex);
        Assert.Equal(TileType.Property, result.TileType);
        Assert.True(result.RequiresAction);
        Assert.False(result.NoAction);
        Assert.Equal(TileResolutionActionKind.PropertyPlaceholder, result.ActionKind);
    }

    [Fact]
    public void ResolveCurrentTile_ReturnsStartPlaceholderForStart()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "start"));

        var result = TileResolver.ResolveCurrentTile(gameState, new PlayerId("player_1"));

        Assert.Equal("start", result.TileId.Value);
        Assert.Equal(0, result.TileIndex);
        Assert.Equal(TileType.Start, result.TileType);
        Assert.False(result.RequiresAction);
        Assert.True(result.NoAction);
        Assert.Equal(TileResolutionActionKind.StartPlaceholder, result.ActionKind);
    }

    [Fact]
    public void ResolveCurrentTile_RejectsUnknownPlayer()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "start"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => TileResolver.ResolveCurrentTile(gameState, new PlayerId("missing_player")));

        Assert.Equal("Player must exist in the game player list before resolving a tile.", exception.Message);
    }

    [Fact]
    public void ResolveCurrentTile_RejectsPlayerOnUnknownTile()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "missing_tile"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => TileResolver.ResolveCurrentTile(gameState, new PlayerId("player_1")));

        Assert.Equal("Player current tile must exist on the board before resolving a tile.", exception.Message);
    }

    [Fact]
    public void ResolveCurrentTile_DoesNotMutateMoneyOwnershipOrGameState()
    {
        var ownedTileId = new TileId("property_02");
        var player = CreatePlayer("player_1", "property_01") with
        {
            Money = new Money(1234),
            OwnedPropertyIds = new HashSet<TileId> { ownedTileId },
        };
        var gameState = CreateGameState(player) with
        {
            Phase = GamePhase.ResolvingTurn,
            TurnNumber = 7,
        };

        _ = TileResolver.ResolveCurrentTile(gameState, new PlayerId("player_1"));

        Assert.Equal(new Money(1234), gameState.Players[0].Money);
        Assert.Contains(ownedTileId, gameState.Players[0].OwnedPropertyIds);
        Assert.Equal("property_01", gameState.Players[0].CurrentTileId.Value);
        Assert.Equal(GamePhase.ResolvingTurn, gameState.Phase);
        Assert.Equal(7, gameState.TurnNumber);
        Assert.Null(gameState.EndedAtUtc);
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

    private static Player CreatePlayer(string playerId, string currentTileId)
    {
        return new Player(
            new PlayerId(playerId),
            playerId,
            $"token_{playerId}",
            $"color_{playerId}",
            new Money(1500),
            new TileId(currentTileId),
            new HashSet<TileId>(),
            new HashSet<CardId>(),
            IsBankrupt: false,
            IsEliminated: false);
    }
}
