namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class TurnManagerTests
{
    [Fact]
    public void StartFirstTurn_SelectsFirstPlayerAndAwaitingRollPhase()
    {
        var gameState = CreateGameState("player_1", "player_2");

        var started = TurnManager.StartFirstTurn(gameState);

        Assert.Equal("player_1", started.CurrentTurnPlayerId?.Value);
        Assert.Equal(GamePhase.AwaitingRoll, started.Phase);
        Assert.Equal(1, started.TurnNumber);
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
    public void AdvanceToNextTurn_SelectsNextPlayerInOrder()
    {
        var gameState = TurnManager.StartFirstTurn(CreateGameState("player_1", "player_2", "player_3"));

        var next = TurnManager.AdvanceToNextTurn(gameState);

        Assert.Equal("player_2", next.CurrentTurnPlayerId?.Value);
        Assert.Equal(2, next.TurnNumber);
        Assert.Equal(GamePhase.AwaitingRoll, next.Phase);
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

    private static Player CreatePlayer(string playerId, TileId startTileId, bool isEliminated = false)
    {
        return new Player(
            new PlayerId(playerId),
            playerId,
            $"token_{playerId}",
            $"color_{playerId}",
            new Money(1500),
            startTileId,
            new HashSet<TileId>(),
            new HashSet<CardId>(),
            IsBankrupt: isEliminated,
            IsEliminated: isEliminated);
    }
}
