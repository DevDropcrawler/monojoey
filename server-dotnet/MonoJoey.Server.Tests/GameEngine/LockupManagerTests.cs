namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class LockupManagerTests
{
    [Fact]
    public void SendToLockup_MovesPlayerToLockupAndMarksLocked()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(
            CreatePlayer(
                playerId.Value,
                "property_03",
                turnState: new PlayerTurnState(
                    JailTurnCount: 2,
                    JailRollAttemptCount: 1,
                    ConsecutiveDoublesCount: 2,
                    LastJailReleaseReason: "held_escape")));

        var result = LockupManager.SendToLockup(gameState, playerId);

        Assert.Equal("lockup_01", result.Players[0].CurrentTileId.Value);
        Assert.True(result.Players[0].IsLockedUp);
        Assert.Equal(0, result.Players[0].TurnState.JailTurnCount);
        Assert.Equal(0, result.Players[0].TurnState.JailRollAttemptCount);
        Assert.Equal(0, result.Players[0].TurnState.ConsecutiveDoublesCount);
        Assert.Null(result.Players[0].TurnState.LastJailReleaseReason);
    }

    [Fact]
    public void SendToLockup_DoesNotCollectPassStartMoney()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "go_to_lockup_01", money: 1500));

        var result = LockupManager.SendToLockup(gameState, playerId);

        Assert.Equal("lockup_01", result.Players[0].CurrentTileId.Value);
        Assert.Equal(new Money(1500), result.Players[0].Money);
    }

    [Fact]
    public void GrantGetOutOfLockupEscape_HoldsEscapeCard()
    {
        var playerId = new PlayerId("player_1");
        var escapeId = new CardId("escape_01");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "start"));

        var result = LockupManager.GrantGetOutOfLockupEscape(gameState, playerId, escapeId);

        Assert.Contains(escapeId, result.Players[0].HeldCardIds);
        Assert.False(result.Players[0].IsLockedUp);
    }

    [Fact]
    public void GrantGetOutOfLockupEscape_WhenEscapeCardsAreDisabledIsNoOp()
    {
        var playerId = new PlayerId("player_1");
        var escapeId = new CardId("escape_01");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "start")) with
        {
            Rules = GameRulesPresets.MonoJoeyDefault with
            {
                Jail = GameRulesPresets.MonoJoeyDefault.Jail with { EscapeCardsEnabled = false },
            },
        };

        var result = LockupManager.GrantGetOutOfLockupEscape(gameState, playerId, escapeId);

        Assert.Same(gameState, result);
        Assert.DoesNotContain(escapeId, result.Players[0].HeldCardIds);
    }

    [Fact]
    public void UseGetOutOfLockupEscape_ClearsLockupAndConsumesHeldEscape()
    {
        var playerId = new PlayerId("player_1");
        var escapeId = new CardId("escape_01");
        var gameState = CreateGameState(
            CreatePlayer(
                playerId.Value,
                "lockup_01",
                heldCardIds: new[] { escapeId },
                isLockedUp: true,
                turnState: new PlayerTurnState(
                    JailTurnCount: 1,
                    JailRollAttemptCount: 1,
                    ConsecutiveDoublesCount: 1)));

        var result = LockupManager.UseGetOutOfLockupEscape(gameState, playerId, escapeId);

        Assert.Equal(LockupEscapeUseResultKind.ClearedLockup, result.Kind);
        Assert.False(result.GameState.Players[0].IsLockedUp);
        Assert.DoesNotContain(escapeId, result.GameState.Players[0].HeldCardIds);
        Assert.Equal(0, result.GameState.Players[0].TurnState.JailTurnCount);
        Assert.Equal(0, result.GameState.Players[0].TurnState.JailRollAttemptCount);
        Assert.Equal(0, result.GameState.Players[0].TurnState.ConsecutiveDoublesCount);
        Assert.Equal("held_escape", result.GameState.Players[0].TurnState.LastJailReleaseReason);
    }

    [Fact]
    public void UseGetOutOfLockupEscape_WhenEscapeCardsAreDisabledIsClearNoOp()
    {
        var playerId = new PlayerId("player_1");
        var escapeId = new CardId("escape_01");
        var gameState = CreateGameState(
            CreatePlayer(
                playerId.Value,
                "lockup_01",
                heldCardIds: new[] { escapeId },
                isLockedUp: true)) with
        {
            Rules = GameRulesPresets.MonoJoeyDefault with
            {
                Jail = GameRulesPresets.MonoJoeyDefault.Jail with { EscapeCardsEnabled = false },
            },
        };

        var result = LockupManager.UseGetOutOfLockupEscape(gameState, playerId, escapeId);

        Assert.Equal(LockupEscapeUseResultKind.EscapeCardsDisabled, result.Kind);
        Assert.Same(gameState, result.GameState);
        Assert.True(result.GameState.Players[0].IsLockedUp);
        Assert.Contains(escapeId, result.GameState.Players[0].HeldCardIds);
    }

    [Fact]
    public void ReleaseFromLockup_ClearsLockupAndStoresExplicitReason()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(
            CreatePlayer(
                playerId.Value,
                "lockup_01",
                isLockedUp: true,
                turnState: new PlayerTurnState(
                    JailTurnCount: 2,
                    JailRollAttemptCount: 2,
                    ConsecutiveDoublesCount: 1)));

        var result = LockupManager.ReleaseFromLockup(gameState, playerId, "paid_fine");

        Assert.False(result.Players[0].IsLockedUp);
        Assert.Equal(0, result.Players[0].TurnState.JailTurnCount);
        Assert.Equal(0, result.Players[0].TurnState.JailRollAttemptCount);
        Assert.Equal(0, result.Players[0].TurnState.ConsecutiveDoublesCount);
        Assert.Equal("paid_fine", result.Players[0].TurnState.LastJailReleaseReason);
    }

    [Fact]
    public void PayFineAndRelease_WhenEligibleDeductsFineAndClearsLockup()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(
            CreatePlayer(playerId.Value, "lockup_01", money: 100, isLockedUp: true));

        var result = LockupManager.PayFineAndRelease(gameState, playerId);

        Assert.Equal(LockupFinePaymentResultKind.PaidAndReleased, result.Kind);
        Assert.Equal(new Money(50), result.FineAmount);
        Assert.Equal(new Money(50), result.GameState.Players[0].Money);
        Assert.False(result.GameState.Players[0].IsLockedUp);
        Assert.Equal("paid_fine", result.GameState.Players[0].TurnState.LastJailReleaseReason);
        Assert.True(LockupManager.CanPayFineToExit(gameState, playerId));
    }

    [Fact]
    public void PayFineAndRelease_WhenDisabledOrInsufficientCashIsClearNoOp()
    {
        var playerId = new PlayerId("player_1");
        var disabledGameState = CreateGameState(
            CreatePlayer(playerId.Value, "lockup_01", money: 100, isLockedUp: true)) with
        {
            Rules = GameRulesPresets.MonoJoeyDefault with
            {
                Jail = GameRulesPresets.MonoJoeyDefault.Jail with { PayToExitEnabled = false },
            },
        };
        var brokeGameState = CreateGameState(
            CreatePlayer(playerId.Value, "lockup_01", money: 10, isLockedUp: true));

        var disabledResult = LockupManager.PayFineAndRelease(disabledGameState, playerId);
        var brokeResult = LockupManager.PayFineAndRelease(brokeGameState, playerId);

        Assert.Equal(LockupFinePaymentResultKind.PayToExitDisabled, disabledResult.Kind);
        Assert.Same(disabledGameState, disabledResult.GameState);
        Assert.False(LockupManager.CanPayFineToExit(disabledGameState, playerId));
        Assert.Equal(LockupFinePaymentResultKind.InsufficientCash, brokeResult.Kind);
        Assert.Same(brokeGameState, brokeResult.GameState);
        Assert.False(LockupManager.CanPayFineToExit(brokeGameState, playerId));
    }

    [Fact]
    public void UseGetOutOfLockupEscape_WhenPlayerIsNotLockedIsClearNoOp()
    {
        var playerId = new PlayerId("player_1");
        var escapeId = new CardId("escape_01");
        var player = CreatePlayer(playerId.Value, "start", heldCardIds: new[] { escapeId });
        var gameState = CreateGameState(player);

        var result = LockupManager.UseGetOutOfLockupEscape(gameState, playerId, escapeId);

        Assert.Equal(LockupEscapeUseResultKind.PlayerNotLockedUp, result.Kind);
        Assert.Same(gameState, result.GameState);
        Assert.Contains(escapeId, result.GameState.Players[0].HeldCardIds);
    }

    [Fact]
    public void UseGetOutOfLockupEscape_WhenEscapeIsNotHeldIsClearNoOp()
    {
        var playerId = new PlayerId("player_1");
        var escapeId = new CardId("escape_01");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "lockup_01", isLockedUp: true));

        var result = LockupManager.UseGetOutOfLockupEscape(gameState, playerId, escapeId);

        Assert.Equal(LockupEscapeUseResultKind.EscapeNotHeld, result.Kind);
        Assert.Same(gameState, result.GameState);
        Assert.True(result.GameState.Players[0].IsLockedUp);
    }

    [Fact]
    public void LockupActions_DoNotClearEliminatedPlayers()
    {
        var playerId = new PlayerId("player_1");
        var escapeId = new CardId("escape_01");
        var gameState = CreateGameState(
            CreatePlayer(
                playerId.Value,
                "go_to_lockup_01",
                heldCardIds: new[] { escapeId },
                isLockedUp: false,
                isEliminated: true));

        var locked = LockupManager.SendToLockup(gameState, playerId);
        var escaped = LockupManager.UseGetOutOfLockupEscape(locked, playerId, escapeId).GameState;

        Assert.True(locked.Players[0].IsBankrupt);
        Assert.True(locked.Players[0].IsEliminated);
        Assert.True(escaped.Players[0].IsBankrupt);
        Assert.True(escaped.Players[0].IsEliminated);
        Assert.False(escaped.Players[0].IsLockedUp);
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
        IEnumerable<CardId>? heldCardIds = null,
        bool isLockedUp = false,
        bool isEliminated = false,
        PlayerTurnState? turnState = null)
    {
        return new Player(
            new PlayerId(playerId),
            playerId,
            $"token_{playerId}",
            $"color_{playerId}",
            new Money(money),
            new TileId(currentTileId),
            new HashSet<TileId>(),
            heldCardIds?.ToHashSet() ?? new HashSet<CardId>(),
            IsBankrupt: isEliminated,
            IsEliminated: isEliminated)
        {
            IsLockedUp = isLockedUp,
            TurnState = turnState ?? PlayerTurnState.Empty,
        };
    }
}
