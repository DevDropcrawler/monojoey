namespace MonoJoey.Server.GameEngine;

public sealed record CardDeckState(
    string DeckId,
    IReadOnlyList<Card> DrawPile,
    IReadOnlyList<Card> DiscardPile)
{
    public static CardDeckState FromDeck(CardDeck deck)
    {
        return new CardDeckState(deck.DeckId, deck.Cards.ToArray(), Array.Empty<Card>());
    }
}
