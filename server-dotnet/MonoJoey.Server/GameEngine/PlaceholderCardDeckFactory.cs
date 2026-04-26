namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class PlaceholderCardDeckFactory
{
    public const int ChanceDeckCardCount = 16;
    public const int TableDeckCardCount = 16;

    public static CardDeck CreateChanceDeck()
    {
        return new CardDeck(
            "chance",
            "Placeholder Chance Deck",
            new[]
            {
                CreateCard("CHANCE_01_MOVE_TO_START", CardActionKind.MoveToStart),
                CreateCard("CHANCE_02_MOVE_TO_EARLY_PROPERTY", CardActionKind.MoveToTile),
                CreateCard("CHANCE_03_MOVE_TO_MID_PROPERTY", CardActionKind.MoveToTile),
                CreateCard("CHANCE_04_MOVE_TO_LATE_PROPERTY", CardActionKind.MoveToTile),
                CreateCard("CHANCE_05_MOVE_TO_NEAREST_TRANSPORT", CardActionKind.MoveToNearestTransport),
                CreateCard("CHANCE_06_MOVE_TO_NEAREST_TRANSPORT", CardActionKind.MoveToNearestTransport),
                CreateCard("CHANCE_07_MOVE_TO_NEAREST_UTILITY", CardActionKind.MoveToNearestUtility),
                CreateCard("CHANCE_08_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank),
                CreateCard("CHANCE_09_RELEASE_FROM_LOCKUP_HOLD", CardActionKind.HoldForLater),
                CreateCard("CHANCE_10_MOVE_RELATIVE_BACK", CardActionKind.MoveRelative),
                CreateCard("CHANCE_11_GO_TO_LOCKUP", CardActionKind.GoToLockup),
                CreateCard("CHANCE_12_PAY_BANK", CardActionKind.PayBank),
                CreateCard("CHANCE_13_PAY_BANK", CardActionKind.PayBank),
                CreateCard("CHANCE_14_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank),
                CreateCard("CHANCE_15_PAY_EVERY_PLAYER", CardActionKind.PayEveryPlayer),
                CreateCard("CHANCE_16_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank),
            });
    }

    public static CardDeck CreateTableDeck()
    {
        return new CardDeck(
            "table",
            "Placeholder Table Deck",
            new[]
            {
                CreateCard("TABLE_01_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank),
                CreateCard("TABLE_02_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank),
                CreateCard("TABLE_03_PAY_BANK", CardActionKind.PayBank),
                CreateCard("TABLE_04_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank),
                CreateCard("TABLE_05_RELEASE_FROM_LOCKUP_HOLD", CardActionKind.HoldForLater),
                CreateCard("TABLE_06_GO_TO_LOCKUP", CardActionKind.GoToLockup),
                CreateCard("TABLE_07_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank),
                CreateCard("TABLE_08_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank),
                CreateCard("TABLE_09_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank),
                CreateCard("TABLE_10_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank),
                CreateCard("TABLE_11_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank),
                CreateCard("TABLE_12_PAY_BANK", CardActionKind.PayBank),
                CreateCard("TABLE_13_PAY_BANK", CardActionKind.PayBank),
                CreateCard("TABLE_14_REPAIR_OWNED_PROPERTIES", CardActionKind.RepairOwnedProperties),
                CreateCard("TABLE_15_RECEIVE_FROM_EVERY_PLAYER", CardActionKind.ReceiveFromEveryPlayer),
                CreateCard("TABLE_16_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank),
            });
    }

    public static IReadOnlyList<CardDeck> CreateAll()
    {
        return new[] { CreateChanceDeck(), CreateTableDeck() };
    }

    private static Card CreateCard(string id, CardActionKind actionKind)
    {
        return new Card(new CardId(id), id, actionKind);
    }
}
