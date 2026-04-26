namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class LoanManagerTests
{
    [Fact]
    public void TakeLoan_IncreasesPlayerMoney()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500));

        var result = LoanManager.TakeLoan(gameState, playerId, new Money(200));

        Assert.True(result.LoanTaken);
        Assert.Equal(LoanTakeResultKind.Accepted, result.ResultKind);
        Assert.Equal(new Money(1700), result.GameState.Players[0].Money);
        Assert.Equal(new Money(1500), gameState.Players[0].Money);
    }

    [Fact]
    public void TakeLoan_CreatesLoanStateWhenNoneExists()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500));

        var result = LoanManager.TakeLoan(gameState, playerId, new Money(100));

        Assert.NotNull(result.LoanState);
        Assert.Equal(new Money(100), result.LoanState.TotalBorrowed);
        Assert.Equal(20, result.LoanState.CurrentInterestRatePercent);
        Assert.Equal(new Money(20), result.LoanState.NextTurnInterestDue);
        Assert.Equal(1, result.LoanState.LoanTier);
        Assert.Same(result.LoanState, result.GameState.Players[0].LoanState);
        Assert.Null(gameState.Players[0].LoanState);
    }

    [Fact]
    public void TakeLoan_MultipleLoansIncreaseInterestRate()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500));

        var firstResult = LoanManager.TakeLoan(gameState, playerId, new Money(100));
        var secondResult = LoanManager.TakeLoan(firstResult.GameState, playerId, new Money(100));

        Assert.NotNull(firstResult.LoanState);
        Assert.NotNull(secondResult.LoanState);
        Assert.True(secondResult.LoanState.CurrentInterestRatePercent > firstResult.LoanState.CurrentInterestRatePercent);
    }

    [Fact]
    public void TakeLoan_EscalatesInterestRateByLoanTier()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500));

        var firstResult = LoanManager.TakeLoan(gameState, playerId, new Money(100));
        var secondResult = LoanManager.TakeLoan(firstResult.GameState, playerId, new Money(100));
        var thirdResult = LoanManager.TakeLoan(secondResult.GameState, playerId, new Money(100));
        var fourthResult = LoanManager.TakeLoan(thirdResult.GameState, playerId, new Money(100));

        Assert.Equal(20, firstResult.LoanState?.CurrentInterestRatePercent);
        Assert.Equal(30, secondResult.LoanState?.CurrentInterestRatePercent);
        Assert.Equal(50, thirdResult.LoanState?.CurrentInterestRatePercent);
        Assert.Equal(60, fourthResult.LoanState?.CurrentInterestRatePercent);
        Assert.Equal(new Money(240), fourthResult.LoanState?.NextTurnInterestDue);
    }

    [Fact]
    public void TakeLoan_PersistsLoanStateAcrossMultipleCalls()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500));

        var firstResult = LoanManager.TakeLoan(gameState, playerId, new Money(125));
        var secondResult = LoanManager.TakeLoan(firstResult.GameState, playerId, new Money(75));

        Assert.Equal(new Money(200), secondResult.GameState.Players[0].LoanState?.TotalBorrowed);
        Assert.Equal(2, secondResult.GameState.Players[0].LoanState?.LoanTier);
        Assert.Equal(30, secondResult.GameState.Players[0].LoanState?.CurrentInterestRatePercent);
        Assert.Equal(new Money(60), secondResult.GameState.Players[0].LoanState?.NextTurnInterestDue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TakeLoan_RejectsInvalidLoanAmount(int amount)
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500));

        var result = LoanManager.TakeLoan(gameState, playerId, new Money(amount));

        Assert.False(result.LoanTaken);
        Assert.Equal(LoanTakeResultKind.InvalidAmount, result.ResultKind);
        Assert.Same(gameState, result.GameState);
        Assert.Equal(new Money(1500), result.GameState.Players[0].Money);
        Assert.Null(result.GameState.Players[0].LoanState);
    }

    [Fact]
    public void TakeLoan_RejectsEliminatedPlayer()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500, isEliminated: true));

        var result = LoanManager.TakeLoan(gameState, playerId, new Money(100));

        Assert.False(result.LoanTaken);
        Assert.Equal(LoanTakeResultKind.PlayerEliminated, result.ResultKind);
        Assert.Same(gameState, result.GameState);
        Assert.Equal(new Money(1500), result.GameState.Players[0].Money);
        Assert.Null(result.GameState.Players[0].LoanState);
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

    private static Player CreatePlayer(string playerId, int money, bool isEliminated = false)
    {
        return new Player(
            new PlayerId(playerId),
            playerId,
            $"token_{playerId}",
            $"color_{playerId}",
            new Money(money),
            new TileId("start"),
            new HashSet<TileId>(),
            new HashSet<CardId>(),
            IsBankrupt: isEliminated,
            IsEliminated: isEliminated);
    }
}
