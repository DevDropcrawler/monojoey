namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;

public class PlaceholderCardDeckFactoryTests
{
    [Fact]
    public void CreateChanceDeck_ReturnsExpectedPlaceholderCardCount()
    {
        var deck = PlaceholderCardDeckFactory.CreateChanceDeck();

        Assert.Equal("chance", deck.DeckId);
        Assert.Equal(PlaceholderCardDeckFactory.ChanceDeckCardCount, deck.Cards.Count);
    }

    [Fact]
    public void CreateTableDeck_ReturnsExpectedPlaceholderCardCount()
    {
        var deck = PlaceholderCardDeckFactory.CreateTableDeck();

        Assert.Equal("table", deck.DeckId);
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
}
