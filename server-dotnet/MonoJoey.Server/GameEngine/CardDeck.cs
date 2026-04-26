namespace MonoJoey.Server.GameEngine;

public sealed record CardDeck(
    string DeckId,
    string DisplayName,
    IReadOnlyList<Card> Cards);
