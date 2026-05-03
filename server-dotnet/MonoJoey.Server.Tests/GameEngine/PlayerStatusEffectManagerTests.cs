namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class PlayerStatusEffectManagerTests
{
    [Fact]
    public void ApplySlimer_AddsDeterministicStatusEffect()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer(playerId));

        var updatedGameState = PlayerStatusEffectManager.ApplySlimer(
            gameState,
            playerId,
            sourceId: "source_card_1");
        var statusEffect = Assert.Single(updatedGameState.Players[0].StatusEffects);

        Assert.Equal("slimer:player_1", statusEffect.InstanceId);
        Assert.Equal(PlayerStatusEffectKind.Slimer, statusEffect.Kind);
        Assert.Equal("slimer", statusEffect.Data.DefinitionId);
        Assert.Equal(1, statusEffect.Data.StackCount);
        Assert.Null(statusEffect.Data.RemainingTurns);
        Assert.Equal("source_card_1", statusEffect.Data.SourceId);
        Assert.True(PlayerStatusEffectManager.HasSlimer(updatedGameState.Players[0]));
    }

    [Fact]
    public void ApplySlimer_WhenAlreadySlimedDoesNotDuplicate()
    {
        var playerId = new PlayerId("player_1");
        var gameState = PlayerStatusEffectManager.ApplySlimer(
            CreateGameState(CreatePlayer(playerId)),
            playerId,
            sourceId: "source_card_1");

        var updatedGameState = PlayerStatusEffectManager.ApplySlimer(
            gameState,
            playerId,
            sourceId: "source_card_2");
        var statusEffect = Assert.Single(updatedGameState.Players[0].StatusEffects);

        Assert.Equal("slimer:player_1", statusEffect.InstanceId);
        Assert.Equal("source_card_1", statusEffect.Data.SourceId);
    }

    [Fact]
    public void RemoveSlimer_RemovesOnlySlimerAndPreservesOtherStatusOrder()
    {
        var playerId = new PlayerId("player_1");
        var firstStatus = new PlayerStatusEffect(
            "status_instance_1",
            PlayerStatusEffectKind.NoOp,
            new PlayerStatusEffectData("status_noop_1"));
        var slimerStatus = new PlayerStatusEffect(
            "slimer:player_1",
            PlayerStatusEffectKind.Slimer,
            new PlayerStatusEffectData("slimer"));
        var secondStatus = new PlayerStatusEffect(
            "status_instance_2",
            PlayerStatusEffectKind.NoOp,
            new PlayerStatusEffectData("status_noop_2"));
        var gameState = CreateGameState(
            CreatePlayer(playerId) with
            {
                StatusEffects = new[] { firstStatus, slimerStatus, secondStatus },
            });

        var updatedGameState = PlayerStatusEffectManager.RemoveSlimer(gameState, playerId);

        Assert.Equal(
            new[] { "status_instance_1", "status_instance_2" },
            updatedGameState.Players[0].StatusEffects.Select(statusEffect => statusEffect.InstanceId).ToArray());
        Assert.False(PlayerStatusEffectManager.HasSlimer(updatedGameState.Players[0]));
    }

    private static GameState CreateGameState(Player player)
    {
        return new GameState(
            new MatchId("match_1"),
            GamePhase.AwaitingRoll,
            DefaultBoardFactory.Create(),
            new[] { player },
            player.PlayerId,
            TurnNumber: 1,
            DateTimeOffset.Parse("2026-05-03T00:00:00+00:00"),
            EndedAtUtc: null);
    }

    private static Player CreatePlayer(PlayerId playerId)
    {
        return new Player(
            playerId,
            "Player 1",
            "token_player_1",
            "color_player_1",
            new Money(1500),
            new TileId("start"),
            new HashSet<TileId>(),
            new HashSet<CardId>(),
            IsBankrupt: false,
            IsEliminated: false);
    }
}
