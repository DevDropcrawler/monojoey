namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class CardEffectExecutorTests
{
    [Fact]
    public void ExecuteCardEffect_MoveToStartMovesPlayerToStartTile()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "tax_01"));
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.MoveToStart,
            new CardActionParameters(TargetTileId: new TileId("start")));

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal("start", result.Players[0].CurrentTileId.Value);
        Assert.Equal(new Money(1500), result.Players[0].Money);
    }

    [Fact]
    public void ExecuteCardEffect_MoveStepsMovesPlayerBackward()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "tax_01"));
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.MoveSteps,
            new CardActionParameters(StepCount: -3));

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal("property_01", result.Players[0].CurrentTileId.Value);
        Assert.Equal(new Money(1500), result.Players[0].Money);
    }

    [Fact]
    public void ExecuteCardEffect_ReceiveMoneyIncreasesPlayerBalance()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "start", money: 100));
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.ReceiveMoney,
            new CardActionParameters(Amount: new Money(50)));

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal(new Money(150), result.Players[0].Money);
        Assert.False(result.Players[0].IsBankrupt);
        Assert.False(result.Players[0].IsEliminated);
    }

    [Fact]
    public void ExecuteCardEffect_PayMoneyDecreasesPlayerBalance()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "start", money: 100));
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.PayMoney,
            new CardActionParameters(Amount: new Money(40)));

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal(new Money(60), result.Players[0].Money);
        Assert.False(result.Players[0].IsBankrupt);
        Assert.False(result.Players[0].IsEliminated);
    }

    [Fact]
    public void ExecuteCardEffect_PayMoneyCausingNegativeBalanceEliminatesPlayer()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "start", money: 10));
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.PayMoney,
            new CardActionParameters(Amount: new Money(15)));

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal(new Money(-5), result.Players[0].Money);
        Assert.True(result.Players[0].IsBankrupt);
        Assert.True(result.Players[0].IsEliminated);
    }

    [Fact]
    public void ExecuteCardEffect_GoToLockupSendsPlayerToLockup()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "go_to_lockup_01"));
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.GoToLockup,
            parameters: null);

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal("lockup_01", result.Players[0].CurrentTileId.Value);
        Assert.True(result.Players[0].IsLockedUp);
        Assert.Equal(new Money(1500), result.Players[0].Money);
    }

    [Fact]
    public void ExecuteCardEffect_GetOutOfLockupStoresHeldEscape()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "start"));
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.GetOutOfLockup,
            parameters: null);

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Contains(cardResolution.CardId, result.Players[0].HeldCardIds);
        Assert.False(result.Players[0].IsLockedUp);
    }

    [Fact]
    public void ExecuteCardEffect_DoesNotMutateOutsideExpectedFields()
    {
        var playerId = new PlayerId("player_1");
        var otherPlayerId = new PlayerId("player_2");
        var ownedTileId = new TileId("property_02");
        var heldCardId = new CardId("held_card_01");
        var player = CreatePlayer(playerId.Value, "start", money: 100) with
        {
            OwnedPropertyIds = new HashSet<TileId> { ownedTileId },
            HeldCardIds = new HashSet<CardId> { heldCardId },
        };
        var otherPlayer = CreatePlayer(otherPlayerId.Value, "tax_01", money: 200);
        var gameState = CreateGameState(player, otherPlayer) with
        {
            Phase = GamePhase.ResolvingTurn,
            TurnNumber = 7,
        };
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.ReceiveMoney,
            new CardActionParameters(Amount: new Money(50)));

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal(new Money(150), result.Players[0].Money);
        Assert.Equal("start", result.Players[0].CurrentTileId.Value);
        Assert.Contains(ownedTileId, result.Players[0].OwnedPropertyIds);
        Assert.Contains(heldCardId, result.Players[0].HeldCardIds);
        Assert.False(result.Players[0].IsBankrupt);
        Assert.False(result.Players[0].IsEliminated);
        Assert.Equal(gameState.Players[1], result.Players[1]);
        Assert.Same(gameState.Board, result.Board);
        Assert.Equal(GamePhase.ResolvingTurn, result.Phase);
        Assert.Equal(gameState.CurrentTurnPlayerId, result.CurrentTurnPlayerId);
        Assert.Equal(7, result.TurnNumber);
        Assert.Equal(gameState.StartedAtUtc, result.StartedAtUtc);
        Assert.Null(result.EndedAtUtc);
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

    private static Player CreatePlayer(string playerId, string currentTileId, int money = 1500)
    {
        return new Player(
            new PlayerId(playerId),
            playerId,
            $"token_{playerId}",
            $"color_{playerId}",
            new Money(money),
            new TileId(currentTileId),
            new HashSet<TileId>(),
            new HashSet<CardId>(),
            IsBankrupt: false,
            IsEliminated: false);
    }

    private static CardResolutionResult CreateCardResolution(
        PlayerId playerId,
        CardResolutionActionKind actionKind,
        CardActionParameters? parameters)
    {
        return new CardResolutionResult(playerId, new CardId($"card_{actionKind}"), actionKind, parameters);
    }
}
