namespace MonoJoey.Server.GameEngine;

public static class CardDeckManager
{
    public static CardDrawResult Draw(CardDeckState deckState)
    {
        if (deckState.DrawPile.Count == 0)
        {
            return new CardDrawResult(CardDrawResultKind.DrawPileEmpty, deckState, DrawnCard: null);
        }

        var drawnCard = deckState.DrawPile[0];
        var remainingDrawPile = deckState.DrawPile.Skip(1).ToArray();
        var nextState = deckState with { DrawPile = remainingDrawPile };

        return new CardDrawResult(CardDrawResultKind.Drawn, nextState, drawnCard);
    }

    public static CardDeckState Discard(CardDeckState deckState, Card card)
    {
        var discardPile = deckState.DiscardPile.ToArray();
        var nextDiscardPile = new Card[discardPile.Length + 1];
        Array.Copy(discardPile, nextDiscardPile, discardPile.Length);
        nextDiscardPile[^1] = card;

        return deckState with { DiscardPile = nextDiscardPile };
    }
}
