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
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "property_03"));

        var result = LockupManager.SendToLockup(gameState, playerId);

        Assert.Equal("lockup_01", result.Players[0].CurrentTileId.Value);
        Assert.True(result.Players[0].IsLockedUp);
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
    public void UseGetOutOfLockupEscape_ClearsLockupAndConsumesHeldEscape()
    {
        var playerId = new PlayerId("player_1");
        var escapeId = new CardId("escape_01");
        var gameState = CreateGameState(
            CreatePlayer(playerId.Value, "lockup_01", heldCardIds: new[] { escapeId }, isLockedUp: true));

        var result = LockupManager.UseGetOutOfLockupEscape(gameState, playerId, escapeId);

        Assert.Equal(LockupEscapeUseResultKind.ClearedLockup, result.Kind);
        Assert.False(result.GameState.Players[0].IsLockedUp);
        Assert.DoesNotContain(escapeId, result.GameState.Players[0].HeldCardIds);
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
        bool isEliminated = false)
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
        };
    }
}
