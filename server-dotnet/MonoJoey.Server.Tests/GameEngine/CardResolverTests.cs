namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class CardResolverTests
{
    [Theory]
    [MemberData(nameof(CardActionMappings))]
    public void ResolveCard_MapsEachSupportedActionKindToResolutionKind(
        CardActionKind sourceActionKind,
        CardActionParameters? sourceParameters,
        CardResolutionActionKind expectedActionKind,
        CardActionParameters? expectedParameters)
    {
        var player = CreatePlayer("player_1", "start");
        var card = CreateCard(sourceActionKind, sourceParameters);

        var result = CardResolver.ResolveCard(player, card);

        Assert.True(result.IsValid);
        Assert.Equal(player.PlayerId, result.PlayerId);
        Assert.Equal(card.CardId, result.CardId);
        Assert.Equal(expectedActionKind, result.ActionKind);
        Assert.Equal(expectedParameters, result.Parameters);
    }

    [Fact]
    public void ResolveCard_PassesCardParametersThroughForParameterizedResult()
    {
        var player = CreatePlayer("player_1", "start");
        var parameters = new CardActionParameters(StepCount: -3);
        var card = CreateCard(CardActionKind.MoveRelative, parameters);

        var result = CardResolver.ResolveCard(player, card);

        Assert.Equal(CardResolutionActionKind.MoveSteps, result.ActionKind);
        Assert.Same(parameters, result.Parameters);
        Assert.Equal(-3, result.Parameters!.StepCount);
    }

    [Fact]
    public void ResolveCard_ReturnsInvalidResultForUnspecifiedCard()
    {
        var player = CreatePlayer("player_1", "start");
        var card = CreateCard(CardActionKind.Unspecified, parameters: null);

        var result = CardResolver.ResolveCard(player, card);

        Assert.False(result.IsValid);
        Assert.Equal(player.PlayerId, result.PlayerId);
        Assert.Equal(card.CardId, result.CardId);
        Assert.Equal(CardResolutionActionKind.InvalidCard, result.ActionKind);
        Assert.Null(result.Parameters);
    }

    [Fact]
    public void ResolveCard_ReturnsInvalidResultWhenRequiredParametersAreMissing()
    {
        var player = CreatePlayer("player_1", "start");
        var card = CreateCard(CardActionKind.ReceiveFromBank, parameters: null);

        var result = CardResolver.ResolveCard(player, card);

        Assert.False(result.IsValid);
        Assert.Equal(CardResolutionActionKind.InvalidCard, result.ActionKind);
        Assert.Null(result.Parameters);
    }

    [Theory]
    [InlineData(null, 50)]
    [InlineData("property_01", null)]
    [InlineData("property_01", -1)]
    [InlineData("property_01", 101)]
    public void ResolveCard_ReturnsInvalidResultWhenEarthquakeParametersAreInvalid(
        string? tileId,
        int? damagePercent)
    {
        var player = CreatePlayer("player_1", "start");
        IReadOnlyList<TileId>? tileIds = tileId is null ? null : new[] { new TileId(tileId) };
        var card = CreateCard(
            CardActionKind.ApplyEarthquake,
            new CardActionParameters(TileIds: tileIds, DamagePercent: damagePercent));

        var result = CardResolver.ResolveCard(player, card);

        Assert.False(result.IsValid);
        Assert.Equal(CardResolutionActionKind.InvalidCard, result.ActionKind);
        Assert.Null(result.Parameters);
    }

    [Fact]
    public void ResolveCard_ReturnsInvalidResultForUndefinedActionKind()
    {
        var player = CreatePlayer("player_1", "start");
        var card = CreateCard((CardActionKind)999, parameters: null);

        var result = CardResolver.ResolveCard(player, card);

        Assert.False(result.IsValid);
        Assert.Equal(CardResolutionActionKind.InvalidCard, result.ActionKind);
        Assert.Null(result.Parameters);
    }

    [Fact]
    public void ResolveCard_DoesNotMutateGameState()
    {
        var ownedTileId = new TileId("property_02");
        var heldCardId = new CardId("held_card_01");
        var player = CreatePlayer("player_1", "property_01") with
        {
            Money = new Money(1234),
            OwnedPropertyIds = new HashSet<TileId> { ownedTileId },
            HeldCardIds = new HashSet<CardId> { heldCardId },
        };
        var gameState = CreateGameState(player) with
        {
            Phase = GamePhase.ResolvingTurn,
            TurnNumber = 7,
        };
        var card = CreateCard(
            CardActionKind.ReceiveFromBank,
            new CardActionParameters(Amount: new Money(500)));

        _ = CardResolver.ResolveCard(gameState.Players[0], card);

        Assert.Equal(new Money(1234), gameState.Players[0].Money);
        Assert.Equal("property_01", gameState.Players[0].CurrentTileId.Value);
        Assert.Contains(ownedTileId, gameState.Players[0].OwnedPropertyIds);
        Assert.Contains(heldCardId, gameState.Players[0].HeldCardIds);
        Assert.False(gameState.Players[0].IsBankrupt);
        Assert.False(gameState.Players[0].IsEliminated);
        Assert.Equal(GamePhase.ResolvingTurn, gameState.Phase);
        Assert.Equal(7, gameState.TurnNumber);
        Assert.Null(gameState.EndedAtUtc);
    }

    [Fact]
    public void ResolveCard_ReturnsValidResultsForPlaceholderDecks()
    {
        var player = CreatePlayer("player_1", "start");
        var cards = PlaceholderCardDeckFactory.CreateAll().SelectMany(deck => deck.Cards);

        foreach (var card in cards)
        {
            var result = CardResolver.ResolveCard(player, card);

            Assert.True(result.IsValid);
            Assert.Equal(player.PlayerId, result.PlayerId);
            Assert.Equal(card.CardId, result.CardId);
        }
    }

    public static IEnumerable<object[]> CardActionMappings()
    {
        yield return new object[]
        {
            CardActionKind.MoveToStart,
            null!,
            CardResolutionActionKind.MoveToStart,
            new CardActionParameters(TargetTileId: new TileId("start")),
        };

        yield return new object[]
        {
            CardActionKind.MoveToTile,
            new CardActionParameters(TargetTileId: new TileId("property_03")),
            CardResolutionActionKind.MoveToTile,
            new CardActionParameters(TargetTileId: new TileId("property_03")),
        };

        yield return new object[]
        {
            CardActionKind.MoveRelative,
            new CardActionParameters(StepCount: -3),
            CardResolutionActionKind.MoveSteps,
            new CardActionParameters(StepCount: -3),
        };

        yield return new object[]
        {
            CardActionKind.MoveToNearestTransport,
            null!,
            CardResolutionActionKind.MoveToNearestTransport,
            null!,
        };

        yield return new object[]
        {
            CardActionKind.MoveToNearestUtility,
            null!,
            CardResolutionActionKind.MoveToNearestUtility,
            null!,
        };

        yield return new object[]
        {
            CardActionKind.ReceiveFromBank,
            new CardActionParameters(Amount: new Money(100)),
            CardResolutionActionKind.ReceiveMoney,
            new CardActionParameters(Amount: new Money(100)),
        };

        yield return new object[]
        {
            CardActionKind.PayBank,
            new CardActionParameters(Amount: new Money(50)),
            CardResolutionActionKind.PayMoney,
            new CardActionParameters(Amount: new Money(50)),
        };

        yield return new object[]
        {
            CardActionKind.ReceiveFromEveryPlayer,
            new CardActionParameters(Amount: new Money(10)),
            CardResolutionActionKind.ReceiveMoneyFromEveryPlayer,
            new CardActionParameters(Amount: new Money(10)),
        };

        yield return new object[]
        {
            CardActionKind.PayEveryPlayer,
            new CardActionParameters(Amount: new Money(20)),
            CardResolutionActionKind.PayMoneyToEveryPlayer,
            new CardActionParameters(Amount: new Money(20)),
        };

        yield return new object[]
        {
            CardActionKind.RepairOwnedProperties,
            new CardActionParameters(Amount: new Money(25)),
            CardResolutionActionKind.RepairOwnedProperties,
            new CardActionParameters(Amount: new Money(25)),
        };

        yield return new object[]
        {
            CardActionKind.ApplySlimer,
            null!,
            CardResolutionActionKind.ApplySlimer,
            null!,
        };

        var earthquakeParameters = new CardActionParameters(
            TileIds: new[]
            {
                new TileId("property_01"),
                new TileId("property_02"),
            },
            DamagePercent: 50);
        yield return new object[]
        {
            CardActionKind.ApplyEarthquake,
            earthquakeParameters,
            CardResolutionActionKind.ApplyEarthquake,
            earthquakeParameters,
        };

        yield return new object[]
        {
            CardActionKind.GoToLockup,
            null!,
            CardResolutionActionKind.GoToLockup,
            null!,
        };

        yield return new object[]
        {
            CardActionKind.HoldForLater,
            null!,
            CardResolutionActionKind.GetOutOfLockup,
            null!,
        };
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

    private static Player CreatePlayer(string playerId, string currentTileId)
    {
        return new Player(
            new PlayerId(playerId),
            playerId,
            $"token_{playerId}",
            $"color_{playerId}",
            new Money(1500),
            new TileId(currentTileId),
            new HashSet<TileId>(),
            new HashSet<CardId>(),
            IsBankrupt: false,
            IsEliminated: false);
    }

    private static Card CreateCard(CardActionKind actionKind, CardActionParameters? parameters)
    {
        return new Card(new CardId($"card_{actionKind}"), actionKind.ToString(), actionKind, parameters);
    }
}
