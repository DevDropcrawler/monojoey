namespace MonoJoey.Server.GameEngine;

public sealed record CardDrawResult(
    CardDrawResultKind Kind,
    CardDeckState DeckState,
    Card? DrawnCard)
{
    public bool Succeeded => Kind == CardDrawResultKind.Drawn;
}
