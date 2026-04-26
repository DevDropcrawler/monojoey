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

        var result = LoanManager.TakeLoan(gameState, playerId, new Money(200), BorrowPurpose.RentPayment);

        Assert.True(result.LoanTaken);
        Assert.Equal(LoanTakeResultKind.Accepted, result.ResultKind);
        Assert.Equal(BorrowPurpose.RentPayment, result.Purpose);
        Assert.Equal(new Money(1700), result.GameState.Players[0].Money);
        Assert.Equal(new Money(1500), gameState.Players[0].Money);
    }

    [Fact]
    public void TakeLoan_CreatesLoanStateWhenNoneExists()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500));

        var result = LoanManager.TakeLoan(gameState, playerId, new Money(100), BorrowPurpose.RentPayment);

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

        var firstResult = LoanManager.TakeLoan(gameState, playerId, new Money(100), BorrowPurpose.RentPayment);
        var secondResult = LoanManager.TakeLoan(firstResult.GameState, playerId, new Money(100), BorrowPurpose.RentPayment);

        Assert.NotNull(firstResult.LoanState);
        Assert.NotNull(secondResult.LoanState);
        Assert.True(secondResult.LoanState.CurrentInterestRatePercent > firstResult.LoanState.CurrentInterestRatePercent);
    }

    [Fact]
    public void TakeLoan_EscalatesInterestRateByLoanTier()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500));

        var firstResult = LoanManager.TakeLoan(gameState, playerId, new Money(100), BorrowPurpose.RentPayment);
        var secondResult = LoanManager.TakeLoan(firstResult.GameState, playerId, new Money(100), BorrowPurpose.RentPayment);
        var thirdResult = LoanManager.TakeLoan(secondResult.GameState, playerId, new Money(100), BorrowPurpose.RentPayment);
        var fourthResult = LoanManager.TakeLoan(thirdResult.GameState, playerId, new Money(100), BorrowPurpose.RentPayment);

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

        var firstResult = LoanManager.TakeLoan(gameState, playerId, new Money(125), BorrowPurpose.RentPayment);
        var secondResult = LoanManager.TakeLoan(firstResult.GameState, playerId, new Money(75), BorrowPurpose.RentPayment);

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

        var result = LoanManager.TakeLoan(gameState, playerId, new Money(amount), BorrowPurpose.RentPayment);

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

        var result = LoanManager.TakeLoan(gameState, playerId, new Money(100), BorrowPurpose.RentPayment);

        Assert.False(result.LoanTaken);
        Assert.Equal(LoanTakeResultKind.PlayerEliminated, result.ResultKind);
        Assert.Same(gameState, result.GameState);
        Assert.Equal(new Money(1500), result.GameState.Players[0].Money);
        Assert.Null(result.GameState.Players[0].LoanState);
    }

    [Theory]
    [InlineData(BorrowPurpose.AuctionBid)]
    [InlineData(BorrowPurpose.RentPayment)]
    [InlineData(BorrowPurpose.TaxPayment)]
    [InlineData(BorrowPurpose.CardPenalty)]
    [InlineData(BorrowPurpose.Fine)]
    public void TakeLoan_AllowsNonLoanPaymentBorrowPurposes(BorrowPurpose purpose)
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500));

        var result = LoanManager.TakeLoan(gameState, playerId, new Money(100), purpose);

        Assert.True(result.LoanTaken);
        Assert.Equal(LoanTakeResultKind.Accepted, result.ResultKind);
        Assert.Equal(purpose, result.Purpose);
        Assert.Equal(new Money(1600), result.GameState.Players[0].Money);
        Assert.Equal(new Money(100), result.GameState.Players[0].LoanState?.TotalBorrowed);
    }

    [Theory]
    [InlineData(BorrowPurpose.LoanInterest)]
    [InlineData(BorrowPurpose.LoanPrincipalRepayment)]
    [InlineData(BorrowPurpose.ExistingLoanDebt)]
    public void TakeLoan_BlocksLoanPaymentBorrowPurposes(BorrowPurpose purpose)
    {
        var playerId = new PlayerId("player_1");
        var loanState = new PlayerLoanState(
            TotalBorrowed: new Money(200),
            CurrentInterestRatePercent: 30,
            NextTurnInterestDue: new Money(60),
            LoanTier: 2);
        var gameState = CreateGameState(CreatePlayer("player_1", money: 50, loanState: loanState));

        var result = LoanManager.TakeLoan(gameState, playerId, new Money(100), purpose);

        Assert.False(result.LoanTaken);
        Assert.Equal(LoanTakeResultKind.DisallowedBorrowPurpose, result.ResultKind);
        Assert.Equal(purpose, result.Purpose);
        Assert.Same(gameState, result.GameState);
        Assert.Equal(new Money(50), result.GameState.Players[0].Money);
        Assert.Same(loanState, result.GameState.Players[0].LoanState);
    }

    [Fact]
    public void StartTurnInterestCheck_WhenPlayerHasNoLoanDoesNotMutateState()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500));

        var result = LoanManager.StartTurnInterestCheck(gameState, playerId);

        Assert.Same(gameState, result);
        Assert.Equal(new Money(1500), result.Players[0].Money);
        Assert.Null(result.Players[0].LoanState);
    }

    [Fact]
    public void StartTurnInterestCheck_DeductsInterestUsingStoredInterestRate()
    {
        var playerId = new PlayerId("player_1");
        var loanState = new PlayerLoanState(
            TotalBorrowed: new Money(200),
            CurrentInterestRatePercent: 30,
            NextTurnInterestDue: Money.Zero,
            LoanTier: 2);
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500, loanState: loanState));

        var result = LoanManager.StartTurnInterestCheck(gameState, playerId);

        Assert.Equal(new Money(1440), result.Players[0].Money);
        Assert.Equal(new Money(60), result.Players[0].LoanState?.NextTurnInterestDue);
        Assert.Equal(30, result.Players[0].LoanState?.CurrentInterestRatePercent);
        Assert.Equal(new Money(1500), gameState.Players[0].Money);
    }

    [Fact]
    public void StartTurnInterestCheck_RoundsInterestWithIntegerMoneyArithmetic()
    {
        var playerId = new PlayerId("player_1");
        var loanState = new PlayerLoanState(
            TotalBorrowed: new Money(101),
            CurrentInterestRatePercent: 20,
            NextTurnInterestDue: Money.Zero,
            LoanTier: 1);
        var gameState = CreateGameState(CreatePlayer("player_1", money: 1500, loanState: loanState));

        var result = LoanManager.StartTurnInterestCheck(gameState, playerId);

        Assert.Equal(new Money(1480), result.Players[0].Money);
        Assert.Equal(new Money(20), result.Players[0].LoanState?.NextTurnInterestDue);
    }

    [Fact]
    public void StartTurnInterestCheck_WhenInterestCannotBePaidEliminatesPlayer()
    {
        var playerId = new PlayerId("player_1");
        var loanState = new PlayerLoanState(
            TotalBorrowed: new Money(100),
            CurrentInterestRatePercent: 20,
            NextTurnInterestDue: Money.Zero,
            LoanTier: 1);
        var gameState = CreateGameState(CreatePlayer("player_1", money: 5, loanState: loanState));

        var result = LoanManager.StartTurnInterestCheck(gameState, playerId);

        Assert.Equal(new Money(-15), result.Players[0].Money);
        Assert.True(result.Players[0].IsBankrupt);
        Assert.True(result.Players[0].IsEliminated);
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
        bool isEliminated = false,
        PlayerLoanState? loanState = null)
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
            IsEliminated: isEliminated)
        {
            LoanState = loanState,
        };
    }
}
