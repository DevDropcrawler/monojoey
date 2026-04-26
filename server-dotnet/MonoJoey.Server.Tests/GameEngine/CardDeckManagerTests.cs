namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;

public class CardDeckManagerTests
{
    [Fact]
    public void Draw_ReturnsExpectedTopCard()
    {
        var firstCard = CreateCard("TEST_01");
        var deckState = CreateDeckState(firstCard, CreateCard("TEST_02"));

        var result = CardDeckManager.Draw(deckState);

        Assert.True(result.Succeeded);
        Assert.Equal(CardDrawResultKind.Drawn, result.Kind);
        Assert.Equal(firstCard, result.DrawnCard);
    }

    [Fact]
    public void Draw_DecreasesDrawPileCount()
    {
        var deckState = CreateDeckState(CreateCard("TEST_01"), CreateCard("TEST_02"), CreateCard("TEST_03"));

        var result = CardDeckManager.Draw(deckState);

        Assert.Equal(2, result.DeckState.DrawPile.Count);
        Assert.Empty(result.DeckState.DiscardPile);
    }

    [Fact]
    public void Discard_IncreasesDiscardPileCount()
    {
        var deckState = CreateDeckState(CreateCard("TEST_01"), CreateCard("TEST_02"));
        var drawResult = CardDeckManager.Draw(deckState);

        var nextState = CardDeckManager.Discard(drawResult.DeckState, drawResult.DrawnCard!);

        Assert.Single(nextState.DiscardPile);
        Assert.Equal(drawResult.DrawnCard, nextState.DiscardPile[0]);
        Assert.Single(nextState.DrawPile);
    }

    [Fact]
    public void Draw_ReturnsEmptyResultWhenDrawPileIsEmpty()
    {
        var deckState = new CardDeckState("test", Array.Empty<Card>(), new[] { CreateCard("DISCARDED_01") });

        var result = CardDeckManager.Draw(deckState);

        Assert.False(result.Succeeded);
        Assert.Equal(CardDrawResultKind.DrawPileEmpty, result.Kind);
        Assert.Null(result.DrawnCard);
        Assert.Same(deckState, result.DeckState);
    }

    [Fact]
    public void Draw_UsesDeterministicDrawPileOrdering()
    {
        var deckState = CreateDeckState(CreateCard("TEST_01"), CreateCard("TEST_02"), CreateCard("TEST_03"));

        var firstDraw = CardDeckManager.Draw(deckState);
        var secondDraw = CardDeckManager.Draw(firstDraw.DeckState);

        Assert.Equal("TEST_01", firstDraw.DrawnCard!.CardId.Value);
        Assert.Equal("TEST_02", secondDraw.DrawnCard!.CardId.Value);
        Assert.Equal("TEST_03", secondDraw.DeckState.DrawPile[0].CardId.Value);
    }

    [Fact]
    public void DrawAndDiscard_DoNotMutatePreviousDeckStateInstances()
    {
        var firstCard = CreateCard("TEST_01");
        var secondCard = CreateCard("TEST_02");
        var deckState = CreateDeckState(firstCard, secondCard);

        var drawResult = CardDeckManager.Draw(deckState);
        var discardedState = CardDeckManager.Discard(drawResult.DeckState, drawResult.DrawnCard!);

        Assert.Equal(new[] { firstCard, secondCard }, deckState.DrawPile);
        Assert.Empty(deckState.DiscardPile);
        Assert.Equal(new[] { secondCard }, drawResult.DeckState.DrawPile);
        Assert.Empty(drawResult.DeckState.DiscardPile);
        Assert.Equal(new[] { secondCard }, discardedState.DrawPile);
        Assert.Equal(new[] { firstCard }, discardedState.DiscardPile);
    }

    [Fact]
    public void FromDeck_CopiesCardsIntoDrawPileWithoutMutatingSourceDeck()
    {
        var deck = PlaceholderCardDeckFactory.CreateChanceDeck();

        var deckState = CardDeckState.FromDeck(deck);
        _ = CardDeckManager.Draw(deckState);

        Assert.Equal(deck.DeckId, deckState.DeckId);
        Assert.Equal(PlaceholderCardDeckFactory.ChanceDeckCardCount, deck.Cards.Count);
        Assert.Equal(deck.Cards, deckState.DrawPile);
        Assert.Empty(deckState.DiscardPile);
    }

    private static CardDeckState CreateDeckState(params Card[] cards)
    {
        return new CardDeckState("test", cards, Array.Empty<Card>());
    }

    private static Card CreateCard(string id)
    {
        return new Card(new CardId(id), id, CardActionKind.ReceiveFromBank);
    }
}
