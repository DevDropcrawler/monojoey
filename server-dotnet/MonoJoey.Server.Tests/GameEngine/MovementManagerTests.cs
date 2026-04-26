namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class MovementManagerTests
{
    [Fact]
    public void MovePlayer_MovesForwardWithoutWrap()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "start"));

        var result = MovementManager.MovePlayer(gameState, new PlayerId("player_1"), 3);

        Assert.Equal(3, result.LandingTileIndex);
        Assert.Equal("property_02", result.LandingTileId.Value);
        Assert.False(result.PassedStart);
        Assert.Equal("property_02", result.GameState.Players[0].CurrentTileId.Value);
    }

    [Fact]
    public void MovePlayer_WrapsAroundBoard()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "go_to_lockup_01"));

        var result = MovementManager.MovePlayer(gameState, new PlayerId("player_1"), 2);

        Assert.Equal(1, result.LandingTileIndex);
        Assert.Equal("property_01", result.LandingTileId.Value);
        Assert.True(result.PassedStart);
        Assert.Equal("property_01", result.GameState.Players[0].CurrentTileId.Value);
    }

    [Fact]
    public void MovePlayer_ReturnsExactLandingPosition()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "property_03"));

        var result = MovementManager.MovePlayer(gameState, new PlayerId("player_1"), 4);

        Assert.Equal(11, result.LandingTileIndex);
        Assert.Equal("go_to_lockup_01", result.LandingTileId.Value);
        Assert.Equal("go_to_lockup_01", result.GameState.Players[0].CurrentTileId.Value);
    }

    [Fact]
    public void MovePlayer_TracksMultipleWraps()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "tax_01"));

        var result = MovementManager.MovePlayer(gameState, new PlayerId("player_1"), 26);

        Assert.Equal(6, result.LandingTileIndex);
        Assert.Equal("free_space_01", result.LandingTileId.Value);
        Assert.True(result.PassedStart);
    }

    [Fact]
    public void MovePlayer_TracksPassStartWhenLandingOnStart()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", "tax_01"));

        var result = MovementManager.MovePlayer(gameState, new PlayerId("player_1"), 8);

        Assert.Equal(0, result.LandingTileIndex);
        Assert.Equal("start", result.LandingTileId.Value);
        Assert.True(result.PassedStart);
    }

    [Fact]
    public void MovePlayer_DoesNotChangeOtherPlayers()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", "start"),
            CreatePlayer("player_2", "tax_01"));

        var result = MovementManager.MovePlayer(gameState, new PlayerId("player_1"), 5);

        Assert.Equal("transport_01", result.GameState.Players[0].CurrentTileId.Value);
        Assert.Equal("tax_01", result.GameState.Players[1].CurrentTileId.Value);
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
            IsBankrupt: false);
    }
}
