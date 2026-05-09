namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class PlayerCashTransferManagerTests
{
    [Fact]
    public void TransferBetweenPlayers_MovesCashBetweenPlayers()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100),
            CreatePlayer("player_2", 50));

        var result = PlayerCashTransferManager.TransferBetweenPlayers(
            gameState,
            new PlayerId("player_1"),
            new PlayerId("player_2"),
            new Money(40));

        Assert.True(result.TransferAccepted);
        Assert.Equal(new Money(60), result.FromPlayerBalance);
        Assert.Equal(new Money(90), result.ToPlayerBalance);
        Assert.Equal(new Money(60), result.GameState.Players[0].Money);
        Assert.Equal(new Money(90), result.GameState.Players[1].Money);
        Assert.Equal(new Money(100), gameState.Players[0].Money);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TransferBetweenPlayers_RejectsNonPositiveAmount(int amount)
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 100), CreatePlayer("player_2", 50));

        var result = PlayerCashTransferManager.TransferBetweenPlayers(
            gameState,
            new PlayerId("player_1"),
            new PlayerId("player_2"),
            new Money(amount));

        Assert.Equal(PlayerCashTransferResultKind.InvalidAmount, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void TransferBetweenPlayers_RejectsInsufficientCash()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 10), CreatePlayer("player_2", 50));

        var result = PlayerCashTransferManager.TransferBetweenPlayers(
            gameState,
            new PlayerId("player_1"),
            new PlayerId("player_2"),
            new Money(11));

        Assert.Equal(PlayerCashTransferResultKind.InsufficientCash, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void TransferBetweenPlayers_RejectsOverflow()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", 10),
            CreatePlayer("player_2", int.MaxValue));

        var result = PlayerCashTransferManager.TransferBetweenPlayers(
            gameState,
            new PlayerId("player_1"),
            new PlayerId("player_2"),
            new Money(1));

        Assert.Equal(PlayerCashTransferResultKind.UnsafeMoneyBalance, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Theory]
    [InlineData("missing", PlayerCashTransferResultKind.PlayerNotInGame)]
    [InlineData("same", PlayerCashTransferResultKind.SamePlayer)]
    [InlineData("bankrupt", PlayerCashTransferResultKind.PlayerBankrupt)]
    [InlineData("eliminated", PlayerCashTransferResultKind.PlayerEliminated)]
    public void TransferBetweenPlayers_RejectsInvalidPlayers(string scenario, PlayerCashTransferResultKind expectedKind)
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100),
            CreatePlayer("player_2", 50));
        var fromPlayerId = new PlayerId("player_1");
        var toPlayerId = new PlayerId("player_2");

        gameState = scenario switch
        {
            "bankrupt" => gameState with
            {
                Players = new[]
                {
                    CreatePlayer("player_1", 100, isBankrupt: true),
                    CreatePlayer("player_2", 50),
                },
            },
            "eliminated" => gameState with
            {
                Players = new[]
                {
                    CreatePlayer("player_1", 100, isEliminated: true),
                    CreatePlayer("player_2", 50),
                },
            },
            _ => gameState,
        };

        if (scenario == "missing")
        {
            toPlayerId = new PlayerId("missing_player");
        }
        else if (scenario == "same")
        {
            toPlayerId = fromPlayerId;
        }

        var result = PlayerCashTransferManager.TransferBetweenPlayers(
            gameState,
            fromPlayerId,
            toPlayerId,
            new Money(10));

        Assert.Equal(expectedKind, result.ResultKind);
        Assert.Same(gameState, result.GameState);
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
        bool isBankrupt = false,
        bool isEliminated = false)
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
            isBankrupt,
            isEliminated);
    }
}
