namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;

public class PlaceholderCardDeckFactoryTests
{
    [Fact]
    public void CreateChanceDeck_ReturnsExpectedPlaceholderCardCount()
    {
        var deck = PlaceholderCardDeckFactory.CreateChanceDeck();

        Assert.Equal(CardDeckIds.Chance, deck.DeckId);
        Assert.Equal(PlaceholderCardDeckFactory.ChanceDeckCardCount, deck.Cards.Count);
    }

    [Fact]
    public void CreateTableDeck_ReturnsExpectedPlaceholderCardCount()
    {
        var deck = PlaceholderCardDeckFactory.CreateTableDeck();

        Assert.Equal(CardDeckIds.Table, deck.DeckId);
        Assert.Equal(PlaceholderCardDeckFactory.TableDeckCardCount, deck.Cards.Count);
    }

    [Fact]
    public void CreateAll_ReturnsUniqueCardIdsAcrossPlaceholderDecks()
    {
        var cardIds = PlaceholderCardDeckFactory.CreateAll()
            .SelectMany(deck => deck.Cards)
            .Select(card => card.CardId)
            .ToArray();

        Assert.Equal(cardIds.Length, cardIds.Distinct().Count());
    }

    [Fact]
    public void CreateAll_ReturnsCardsWithValidActionKinds()
    {
        var cards = PlaceholderCardDeckFactory.CreateAll().SelectMany(deck => deck.Cards);

        Assert.All(cards, card =>
        {
            Assert.True(Enum.IsDefined(card.ActionKind));
            Assert.NotEqual(CardActionKind.Unspecified, card.ActionKind);
        });
    }

    [Fact]
    public void CreateAll_UsesFunctionalPlaceholderNamesOnly()
    {
        var cards = PlaceholderCardDeckFactory.CreateAll().SelectMany(deck => deck.Cards);

        Assert.All(cards, card => Assert.Equal(card.CardId.Value, card.DisplayName));
    }

    [Fact]
    public void CreateAll_IncludesInternalSlimerAndEarthquakeCardsWithoutChangingDeckSize()
    {
        var chanceDeck = PlaceholderCardDeckFactory.CreateChanceDeck();
        var tableDeck = PlaceholderCardDeckFactory.CreateTableDeck();

        var slimerCard = Assert.Single(chanceDeck.Cards, card => card.CardId.Value == "CHANCE_06_APPLY_SLIMER");
        var earthquakeCard = Assert.Single(tableDeck.Cards, card => card.CardId.Value == "TABLE_10_APPLY_EARTHQUAKE");

        Assert.Equal(PlaceholderCardDeckFactory.ChanceDeckCardCount, chanceDeck.Cards.Count);
        Assert.Equal(PlaceholderCardDeckFactory.TableDeckCardCount, tableDeck.Cards.Count);
        Assert.Equal(CardActionKind.ApplySlimer, slimerCard.ActionKind);
        Assert.Equal(CardActionKind.ApplyEarthquake, earthquakeCard.ActionKind);
        Assert.Equal(50, earthquakeCard.Parameters!.DamagePercent);
        Assert.Equal(
            new[] { "property_01", "property_02", "property_03", "transport_01", "utility_01" },
            earthquakeCard.Parameters.TileIds!.Select(tileId => tileId.Value).ToArray());
    }
}
