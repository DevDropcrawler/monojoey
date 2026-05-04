namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class PlaceholderCardDeckFactory
{
    public const int ChanceDeckCardCount = 16;
    public const int TableDeckCardCount = 16;

    public static CardDeck CreateChanceDeck()
    {
        return new CardDeck(
            CardDeckIds.Chance,
            "Placeholder Chance Deck",
            new[]
            {
                CreateCard("CHANCE_01_MOVE_TO_START", CardActionKind.MoveToStart),
                CreateMoveToTileCard("CHANCE_02_MOVE_TO_EARLY_PROPERTY", "property_01"),
                CreateMoveToTileCard("CHANCE_03_MOVE_TO_MID_PROPERTY", "property_03"),
                CreateMoveToTileCard("CHANCE_04_MOVE_TO_LATE_PROPERTY", "transport_01"),
                CreateCard("CHANCE_05_MOVE_TO_NEAREST_TRANSPORT", CardActionKind.MoveToNearestTransport),
                CreateCard("CHANCE_06_APPLY_SLIMER", CardActionKind.ApplySlimer),
                CreateCard("CHANCE_07_MOVE_TO_NEAREST_UTILITY", CardActionKind.MoveToNearestUtility),
                CreateMoneyCard("CHANCE_08_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank, 50),
                CreateCard("CHANCE_09_RELEASE_FROM_LOCKUP_HOLD", CardActionKind.HoldForLater),
                CreateMoveRelativeCard("CHANCE_10_MOVE_RELATIVE_BACK", -3),
                CreateCard("CHANCE_11_GO_TO_LOCKUP", CardActionKind.GoToLockup),
                CreateMoneyCard("CHANCE_12_PAY_BANK", CardActionKind.PayBank, 15),
                CreateMoneyCard("CHANCE_13_PAY_BANK", CardActionKind.PayBank, 50),
                CreateMoneyCard("CHANCE_14_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank, 150),
                CreateMoneyCard("CHANCE_15_PAY_EVERY_PLAYER", CardActionKind.PayEveryPlayer, 20),
                CreateMoneyCard("CHANCE_16_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank, 100),
            });
    }

    public static CardDeck CreateTableDeck()
    {
        return new CardDeck(
            CardDeckIds.Table,
            "Placeholder Table Deck",
            new[]
            {
                CreateMoneyCard("TABLE_01_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank, 25),
                CreateMoneyCard("TABLE_02_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank, 50),
                CreateMoneyCard("TABLE_03_PAY_BANK", CardActionKind.PayBank, 50),
                CreateMoneyCard("TABLE_04_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank, 100),
                CreateCard("TABLE_05_RELEASE_FROM_LOCKUP_HOLD", CardActionKind.HoldForLater),
                CreateCard("TABLE_06_GO_TO_LOCKUP", CardActionKind.GoToLockup),
                CreateMoneyCard("TABLE_07_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank, 20),
                CreateMoneyCard("TABLE_08_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank, 10),
                CreateMoneyCard("TABLE_09_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank, 100),
                CreateEarthquakeCard("TABLE_10_APPLY_EARTHQUAKE", 50),
                CreateMoneyCard("TABLE_11_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank, 100),
                CreateMoneyCard("TABLE_12_PAY_BANK", CardActionKind.PayBank, 100),
                CreateMoneyCard("TABLE_13_PAY_BANK", CardActionKind.PayBank, 150),
                CreateMoneyCard("TABLE_14_REPAIR_OWNED_PROPERTIES", CardActionKind.RepairOwnedProperties, 25),
                CreateMoneyCard("TABLE_15_RECEIVE_FROM_EVERY_PLAYER", CardActionKind.ReceiveFromEveryPlayer, 10),
                CreateMoneyCard("TABLE_16_RECEIVE_FROM_BANK", CardActionKind.ReceiveFromBank, 200),
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

    private static Card CreateMoveToTileCard(string id, string targetTileId)
    {
        return new Card(
            new CardId(id),
            id,
            CardActionKind.MoveToTile,
            new CardActionParameters(TargetTileId: new TileId(targetTileId)));
    }

    private static Card CreateMoveRelativeCard(string id, int stepCount)
    {
        return new Card(
            new CardId(id),
            id,
            CardActionKind.MoveRelative,
            new CardActionParameters(StepCount: stepCount));
    }

    private static Card CreateMoneyCard(string id, CardActionKind actionKind, int amount)
    {
        return new Card(
            new CardId(id),
            id,
            actionKind,
            new CardActionParameters(Amount: new Money(amount)));
    }

    private static Card CreateEarthquakeCard(string id, int damagePercent)
    {
        return new Card(
            new CardId(id),
            id,
            CardActionKind.ApplyEarthquake,
            new CardActionParameters(
                TileIds: new[]
                {
                    new TileId("property_01"),
                    new TileId("property_02"),
                    new TileId("property_03"),
                    new TileId("transport_01"),
                    new TileId("utility_01"),
                },
                DamagePercent: damagePercent));
    }
}
