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
    public void ExecuteCardEffect_MoveToTileMovesPlayerForwardToTarget()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "tax_01"));
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.MoveToTile,
            new CardActionParameters(TargetTileId: new TileId("property_01")));

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal("property_01", result.Players[0].CurrentTileId.Value);
        Assert.Equal(new Money(1500), result.Players[0].Money);
    }

    [Fact]
    public void ExecuteCardEffect_MoveToNearestTransportMovesForwardToNearestTransport()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "property_01"));
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.MoveToNearestTransport,
            parameters: null);

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal("transport_01", result.Players[0].CurrentTileId.Value);
    }

    [Fact]
    public void ExecuteCardEffect_MoveToNearestUtilityMovesForwardToNearestUtility()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "transport_01"));
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.MoveToNearestUtility,
            parameters: null);

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal("utility_01", result.Players[0].CurrentTileId.Value);
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
    public void ExecuteCardEffect_ReceiveMoneyFromEveryPlayerTransfersFromActiveOpponents()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(
            CreatePlayer(playerId.Value, "start", money: 100),
            CreatePlayer("player_2", "start", money: 100),
            CreatePlayer("player_3", "start", money: 100));
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.ReceiveMoneyFromEveryPlayer,
            new CardActionParameters(Amount: new Money(10)));

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal(new Money(120), result.Players[0].Money);
        Assert.Equal(new Money(90), result.Players[1].Money);
        Assert.Equal(new Money(90), result.Players[2].Money);
    }

    [Fact]
    public void ExecuteCardEffect_PayMoneyToEveryPlayerTransfersToActiveOpponents()
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(
            CreatePlayer(playerId.Value, "start", money: 100),
            CreatePlayer("player_2", "start", money: 100),
            CreatePlayer("player_3", "start", money: 100));
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.PayMoneyToEveryPlayer,
            new CardActionParameters(Amount: new Money(10)));

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal(new Money(80), result.Players[0].Money);
        Assert.Equal(new Money(110), result.Players[1].Money);
        Assert.Equal(new Money(110), result.Players[2].Money);
    }

    [Fact]
    public void ExecuteCardEffect_RepairOwnedPropertiesChargesOwnedPropertyCountWithoutUpgrades()
    {
        var playerId = new PlayerId("player_1");
        var player = CreatePlayer(playerId.Value, "start", money: 100) with
        {
            OwnedPropertyIds = new HashSet<TileId>
            {
                new("property_01"),
                new("property_02"),
            },
        };
        var gameState = CreateGameState(player);
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.RepairOwnedProperties,
            new CardActionParameters(Amount: new Money(25)));

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal(new Money(50), result.Players[0].Money);
    }

    [Fact]
    public void ExecuteCardEffect_ApplySlimerAddsDeterministicStatusEffectWithCardSource()
    {
        var playerId = new PlayerId("player_1");
        var cardId = new CardId("TEST_APPLY_SLIMER");
        var gameState = CreateGameState(CreatePlayer(playerId.Value, "start"));
        var cardResolution = new CardResolutionResult(
            playerId,
            cardId,
            CardResolutionActionKind.ApplySlimer,
            Parameters: null);

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        var statusEffect = Assert.Single(result.Players[0].StatusEffects);
        Assert.Equal("slimer:player_1", statusEffect.InstanceId);
        Assert.Equal(PlayerStatusEffectKind.Slimer, statusEffect.Kind);
        Assert.Equal("slimer", statusEffect.Data.DefinitionId);
        Assert.Equal(cardId.Value, statusEffect.Data.SourceId);
        Assert.Equal(new Money(1500), result.Players[0].Money);
    }

    [Fact]
    public void ExecuteCardEffect_ApplyEarthquakeDamagesParameterizedTilesOnly()
    {
        var playerId = new PlayerId("player_1");
        var player = CreatePlayer(playerId.Value, "start") with
        {
            OwnedPropertyIds = new HashSet<TileId>
            {
                new("property_01"),
                new("property_03"),
            },
        };
        var gameState = CreateGameState(player);
        var cardResolution = CreateCardResolution(
            playerId,
            CardResolutionActionKind.ApplyEarthquake,
            new CardActionParameters(
                TileIds: new[]
                {
                    new TileId("property_03"),
                    new TileId("property_01"),
                    new TileId("property_03"),
                    new TileId("free_space_01"),
                },
                DamagePercent: 50));

        var result = CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution);

        Assert.Equal(
            new[] { "property_01", "property_03" },
            result.PropertyStates.Keys.Select(tileId => tileId.Value).ToArray());
        Assert.All(result.PropertyStates.Values, propertyState => Assert.Equal(50, propertyState.Data.DamagePercent));
        Assert.Equal(gameState.Players, result.Players);
        Assert.Empty(gameState.PropertyStates);
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
    public void ExecuteCardEffect_AllDefaultPlaceholderCardsExecute()
    {
        var playerId = new PlayerId("player_1");
        var cards = PlaceholderCardDeckFactory.CreateAll().SelectMany(deck => deck.Cards);

        foreach (var card in cards)
        {
            var player = CreatePlayer(playerId.Value, "property_02", money: 1500) with
            {
                OwnedPropertyIds = new HashSet<TileId> { new("property_01"), new("property_03") },
            };
            var gameState = CreateGameState(
                player,
                CreatePlayer("player_2", "start", money: 1500),
                CreatePlayer("player_3", "start", money: 1500));
            var resolution = CardResolver.ResolveCard(player, card);

            var result = CardEffectExecutor.ExecuteCardEffect(gameState, resolution);

            Assert.True(resolution.IsValid);
            Assert.Contains(result.Players, updatedPlayer => updatedPlayer.PlayerId == playerId);
        }
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
