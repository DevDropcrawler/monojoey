namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class BankruptcyManagerTests
{
    [Fact]
    public void EliminateIfBankrupt_MarksNegativeBalancePlayerAsEliminated()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", money: -1));

        var result = BankruptcyManager.EliminateIfBankrupt(gameState, new PlayerId("player_1"));

        Assert.True(result.WasEliminated);
        Assert.Equal(EliminationReason.NegativeBalance, result.Reason);
        Assert.Equal(new Money(-1), result.Balance);
        Assert.Null(result.PaymentDue);
        Assert.True(result.GameState.Players[0].IsBankrupt);
        Assert.True(result.GameState.Players[0].IsEliminated);
        Assert.Equal(new Money(-1), result.GameState.Players[0].Money);
    }

    [Fact]
    public void EliminateIfBankrupt_DoesNotEliminateNonNegativeBalancePlayer()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", money: 0));

        var result = BankruptcyManager.EliminateIfBankrupt(gameState, new PlayerId("player_1"));

        Assert.False(result.WasEliminated);
        Assert.False(result.GameState.Players[0].IsBankrupt);
        Assert.False(result.GameState.Players[0].IsEliminated);
    }

    [Fact]
    public void EliminateForFailedPayment_MarksPlayerAsEliminatedWhenPaymentCannotBeFulfilled()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", money: 5));

        var result = BankruptcyManager.EliminateForFailedPayment(
            gameState,
            new PlayerId("player_1"),
            new Money(6));

        Assert.True(result.WasEliminated);
        Assert.Equal(EliminationReason.CannotFulfillPayment, result.Reason);
        Assert.Equal(new Money(6), result.PaymentDue);
        Assert.Equal(new Money(5), result.PaymentAvailable);
        Assert.True(result.GameState.Players[0].IsBankrupt);
        Assert.True(result.GameState.Players[0].IsEliminated);
        Assert.Equal(new Money(5), result.GameState.Players[0].Money);
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

    private static Player CreatePlayer(string playerId, int money)
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
            IsBankrupt: false,
            IsEliminated: false);
    }
}
