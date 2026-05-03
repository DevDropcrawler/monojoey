namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class GameCompletionManagerTests
{
    private static readonly DateTimeOffset StartedAtUtc = DateTimeOffset.Parse("2026-04-26T00:00:00+00:00");
    private static readonly DateTimeOffset EndedAtUtc = DateTimeOffset.Parse("2026-04-26T01:00:00+00:00");

    [Fact]
    public void CompleteIfWinner_LastPlayerStandingWithMultipleActivePlayersDoesNotComplete()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1"),
            CreatePlayer("player_2"));

        var result = GameCompletionManager.CompleteIfWinner(gameState, EndedAtUtc);

        Assert.Same(gameState, result);
        Assert.Equal(GameStatus.InProgress, result.Status);
        Assert.Null(result.WinnerPlayerId);
        Assert.Null(result.EndedAtUtc);
    }

    [Fact]
    public void CompleteIfWinner_LastPlayerStandingWithOneActivePlayerCompletesWithWinner()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1"),
            CreatePlayer("player_2", isBankrupt: true, isEliminated: true)) with
        {
            Rules = GameRulesPresets.MonoJoeyDefault with
            {
                Win = new WinRules(WinRules.LastPlayerStandingConditionType),
            },
        };

        var result = GameCompletionManager.CompleteIfWinner(gameState, EndedAtUtc);

        Assert.NotSame(gameState, result);
        Assert.Equal(GameStatus.Completed, result.Status);
        Assert.Equal(GamePhase.Completed, result.Phase);
        Assert.Equal(new PlayerId("player_1"), result.WinnerPlayerId);
        Assert.Equal(EndedAtUtc, result.EndedAtUtc);
    }

    [Fact]
    public void CompleteIfWinner_LastPlayerStandingWithZeroActivePlayersDoesNotComplete()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", isBankrupt: true, isEliminated: true),
            CreatePlayer("player_2", isBankrupt: true, isEliminated: true));

        var result = GameCompletionManager.CompleteIfWinner(gameState, EndedAtUtc);

        Assert.Same(gameState, result);
        Assert.Equal(GameStatus.InProgress, result.Status);
        Assert.Null(result.WinnerPlayerId);
    }

    [Fact]
    public void CompleteIfWinner_UnsupportedWinConditionDoesNotComplete()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1"),
            CreatePlayer("player_2", isBankrupt: true, isEliminated: true)) with
        {
            Rules = GameRulesPresets.MonoJoeyDefault with
            {
                Win = new WinRules("futureCondition"),
            },
        };

        var result = GameCompletionManager.CompleteIfWinner(gameState, EndedAtUtc);

        Assert.Same(gameState, result);
        Assert.Equal(GameStatus.InProgress, result.Status);
        Assert.Null(result.WinnerPlayerId);
        Assert.Null(result.EndedAtUtc);
    }

    [Fact]
    public void CompleteIfWinner_CompletedStateIsIdempotent()
    {
        var gameState = CreateGameState(CreatePlayer("player_1")) with
        {
            Status = GameStatus.Completed,
            Phase = GamePhase.Completed,
            WinnerPlayerId = new PlayerId("player_1"),
            EndedAtUtc = StartedAtUtc,
        };

        var result = GameCompletionManager.CompleteIfWinner(gameState, EndedAtUtc);

        Assert.Same(gameState, result);
        Assert.Equal(StartedAtUtc, result.EndedAtUtc);
    }

    [Fact]
    public void CompleteIfWinner_ClearsActiveAuctionStateOnCompletion()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1"),
            CreatePlayer("player_2", isBankrupt: true, isEliminated: true)) with
        {
            ActiveAuctionState = new AuctionState(
                new TileId("property_01"),
                new PlayerId("player_1"),
                AuctionStatus.ActiveBidCountdown,
                Money.Zero,
                new Money(1),
                9,
                3,
                new[]
                {
                    new AuctionBid(new PlayerId("player_1"), new Money(10), StartedAtUtc),
                },
                new Money(10),
                new PlayerId("player_1"),
                CountdownDurationSeconds: 3,
                TimerEndsAtUtc: StartedAtUtc.AddSeconds(3)),
        };

        var result = GameCompletionManager.CompleteIfWinner(gameState, EndedAtUtc);

        Assert.Equal(GameStatus.Completed, result.Status);
        Assert.Null(result.ActiveAuctionState);
    }

    [Fact]
    public void CompleteIfWinner_KeepsActiveAuctionStateWithoutCompletion()
    {
        var activeAuctionState = CreateActiveAuctionState();
        var gameState = CreateGameState(
            CreatePlayer("player_1"),
            CreatePlayer("player_2")) with
        {
            ActiveAuctionState = activeAuctionState,
        };

        var result = GameCompletionManager.CompleteIfWinner(gameState, EndedAtUtc);

        Assert.Same(gameState, result);
        Assert.Same(activeAuctionState, result.ActiveAuctionState);
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
            StartedAtUtc,
            EndedAtUtc: null);
    }

    private static AuctionState CreateActiveAuctionState()
    {
        return new AuctionState(
            new TileId("property_01"),
            new PlayerId("player_1"),
            AuctionStatus.ActiveBidCountdown,
            Money.Zero,
            new Money(1),
            9,
            3,
            new[]
            {
                new AuctionBid(new PlayerId("player_1"), new Money(10), StartedAtUtc),
            },
            new Money(10),
            new PlayerId("player_1"),
            CountdownDurationSeconds: 3,
            TimerEndsAtUtc: StartedAtUtc.AddSeconds(3));
    }

    private static Player CreatePlayer(
        string playerId,
        bool isBankrupt = false,
        bool isEliminated = false)
    {
        return new Player(
            new PlayerId(playerId),
            playerId,
            $"token_{playerId}",
            $"color_{playerId}",
            new Money(1500),
            new TileId("start"),
            new HashSet<TileId>(),
            new HashSet<CardId>(),
            isBankrupt,
            isEliminated);
    }
}
