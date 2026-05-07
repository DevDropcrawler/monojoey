namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;

public class PlaceholderCardDeckFactoryTests
{
    private static readonly string[] ReplacementCardIds =
    {
        "CHANCE_03_MOVE_TO_TABLE",
        "CHANCE_04_MOVE_TO_LOCKUP_VISIT",
        "CHANCE_08_RECEIVE_MEDIUM_BANK_BONUS",
        "CHANCE_12_REPAIR_ASSESSMENT_SMALL",
        "CHANCE_13_LIGHT_EARTHQUAKE",
        "CHANCE_16_PAY_BANK_MEDIUM",
        "TABLE_02_PAY_BANK_SMALL",
        "TABLE_07_MOVE_TO_PROPERTY_02",
        "TABLE_08_MOVE_TO_TRANSPORT",
        "TABLE_11_APPLY_SLIMER",
    };

    private static readonly string[] ReplacedPlaceholderCardIds =
    {
        "CHANCE_03_MOVE_TO_MID_PROPERTY",
        "CHANCE_04_MOVE_TO_LATE_PROPERTY",
        "CHANCE_08_RECEIVE_FROM_BANK",
        "CHANCE_12_PAY_BANK",
        "CHANCE_13_PAY_BANK",
        "CHANCE_16_RECEIVE_FROM_BANK",
        "TABLE_02_RECEIVE_FROM_BANK",
        "TABLE_07_RECEIVE_FROM_BANK",
        "TABLE_08_RECEIVE_FROM_BANK",
        "TABLE_11_RECEIVE_FROM_BANK",
    };

    private static readonly CardActionKind[] ReplacementAllowedActionKinds =
    {
        CardActionKind.MoveToTile,
        CardActionKind.ReceiveFromBank,
        CardActionKind.PayBank,
        CardActionKind.ApplySlimer,
        CardActionKind.ApplyEarthquake,
        CardActionKind.RepairOwnedProperties,
        CardActionKind.GoToLockup,
    };

    private static readonly string[] ExpandedEarthquakeTileIds =
    {
        "property_01",
        "property_02",
        "property_03",
        "transport_01",
        "utility_01",
    };

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

    [Fact]
    public void CreateAll_IncludesCardExpansionReplacementCardsWithoutPlaceholderIds()
    {
        var cards = PlaceholderCardDeckFactory.CreateAll()
            .SelectMany(deck => deck.Cards)
            .ToArray();
        var cardIds = cards.Select(card => card.CardId.Value).ToArray();

        foreach (var replacementCardId in ReplacementCardIds)
        {
            Assert.Single(cardIds, cardId => cardId == replacementCardId);
        }

        foreach (var replacedPlaceholderCardId in ReplacedPlaceholderCardIds)
        {
            Assert.DoesNotContain(replacedPlaceholderCardId, cardIds);
        }

        var replacementCards = cards
            .Where(card => ReplacementCardIds.Contains(card.CardId.Value, StringComparer.Ordinal))
            .ToArray();

        Assert.Equal(ReplacementCardIds.Length, replacementCards.Length);
        Assert.All(
            replacementCards,
            card => Assert.Contains(card.ActionKind, ReplacementAllowedActionKinds));
    }

    [Fact]
    public void CreateAll_UsesExpectedCardExpansionReplacementParameters()
    {
        var cards = PlaceholderCardDeckFactory.CreateAll()
            .SelectMany(deck => deck.Cards)
            .ToDictionary(card => card.CardId.Value, StringComparer.Ordinal);

        AssertMoveToTile(cards["CHANCE_03_MOVE_TO_TABLE"], "table_01");
        AssertMoveToTile(cards["CHANCE_04_MOVE_TO_LOCKUP_VISIT"], "lockup_01");
        AssertMoney(cards["CHANCE_08_RECEIVE_MEDIUM_BANK_BONUS"], CardActionKind.ReceiveFromBank, 75);
        AssertMoney(cards["CHANCE_12_REPAIR_ASSESSMENT_SMALL"], CardActionKind.RepairOwnedProperties, 15);
        AssertEarthquake(cards["CHANCE_13_LIGHT_EARTHQUAKE"], 25);
        AssertMoney(cards["CHANCE_16_PAY_BANK_MEDIUM"], CardActionKind.PayBank, 75);
        AssertMoney(cards["TABLE_02_PAY_BANK_SMALL"], CardActionKind.PayBank, 25);
        AssertMoveToTile(cards["TABLE_07_MOVE_TO_PROPERTY_02"], "property_02");
        AssertMoveToTile(cards["TABLE_08_MOVE_TO_TRANSPORT"], "transport_01");

        var slimerCard = cards["TABLE_11_APPLY_SLIMER"];
        Assert.Equal(CardActionKind.ApplySlimer, slimerCard.ActionKind);
        Assert.Null(slimerCard.Parameters);
    }

    [Fact]
    public void CreatePreset_ClassicIshMatchesDefaultDeckOrder()
    {
        var preset = PlaceholderCardDeckFactory.CreatePreset(CardDeckPresetIds.ClassicIsh);
        var defaultDecks = PlaceholderCardDeckFactory.CreateAll();

        Assert.Equal(
            defaultDecks.Select(deck => deck.DeckId).ToArray(),
            preset.Select(deck => deck.DeckId).ToArray());
        Assert.Equal(
            defaultDecks.SelectMany(deck => deck.Cards.Select(card => card.CardId.Value)).ToArray(),
            preset.SelectMany(deck => deck.Cards.Select(card => card.CardId.Value)).ToArray());
    }

    [Fact]
    public void CreatePreset_ChaosPreservesDeckIdsAndSizesWithAlternateOrder()
    {
        var preset = PlaceholderCardDeckFactory.CreatePreset(CardDeckPresetIds.Chaos);
        var chanceDeck = Assert.Single(preset, deck => deck.DeckId == CardDeckIds.Chance);
        var tableDeck = Assert.Single(preset, deck => deck.DeckId == CardDeckIds.Table);

        Assert.Equal(PlaceholderCardDeckFactory.ChanceDeckCardCount, chanceDeck.Cards.Count);
        Assert.Equal(PlaceholderCardDeckFactory.TableDeckCardCount, tableDeck.Cards.Count);
        Assert.Equal("CHANCE_11_GO_TO_LOCKUP", chanceDeck.Cards[0].CardId.Value);
        Assert.Equal("TABLE_06_GO_TO_LOCKUP", tableDeck.Cards[0].CardId.Value);
        Assert.Equal(
            PlaceholderCardDeckFactory.CreateChanceDeck().Cards.Select(card => card.CardId.Value).OrderBy(cardId => cardId),
            chanceDeck.Cards.Select(card => card.CardId.Value).OrderBy(cardId => cardId));
        Assert.Equal(
            PlaceholderCardDeckFactory.CreateTableDeck().Cards.Select(card => card.CardId.Value).OrderBy(cardId => cardId),
            tableDeck.Cards.Select(card => card.CardId.Value).OrderBy(cardId => cardId));
    }

    [Fact]
    public void CreatePreset_CustomReadyCurrentlyMatchesClassicIsh()
    {
        var classic = PlaceholderCardDeckFactory.CreatePreset(CardDeckPresetIds.ClassicIsh);
        var customReady = PlaceholderCardDeckFactory.CreatePreset(CardDeckPresetIds.CustomReady);

        Assert.Equal(
            classic.SelectMany(deck => deck.Cards.Select(card => card.CardId.Value)).ToArray(),
            customReady.SelectMany(deck => deck.Cards.Select(card => card.CardId.Value)).ToArray());
    }

    [Fact]
    public void CreatePreset_RejectsUnknownPresetId()
    {
        Assert.Throws<ArgumentException>(() => PlaceholderCardDeckFactory.CreatePreset("missing"));
    }

    private static void AssertMoveToTile(Card card, string targetTileId)
    {
        Assert.Equal(CardActionKind.MoveToTile, card.ActionKind);
        Assert.Equal(targetTileId, card.Parameters!.TargetTileId!.Value.Value);
        Assert.Null(card.Parameters.Amount);
        Assert.Null(card.Parameters.StepCount);
        Assert.Null(card.Parameters.TileIds);
        Assert.Null(card.Parameters.DamagePercent);
    }

    private static void AssertMoney(Card card, CardActionKind actionKind, int amount)
    {
        Assert.Equal(actionKind, card.ActionKind);
        Assert.Equal(amount, card.Parameters!.Amount!.Value.Amount);
        Assert.Null(card.Parameters.TargetTileId);
        Assert.Null(card.Parameters.StepCount);
        Assert.Null(card.Parameters.TileIds);
        Assert.Null(card.Parameters.DamagePercent);
    }

    private static void AssertEarthquake(Card card, int damagePercent)
    {
        Assert.Equal(CardActionKind.ApplyEarthquake, card.ActionKind);
        Assert.Equal(damagePercent, card.Parameters!.DamagePercent);
        Assert.Equal(ExpandedEarthquakeTileIds, card.Parameters.TileIds!.Select(tileId => tileId.Value).ToArray());
        Assert.Null(card.Parameters.TargetTileId);
        Assert.Null(card.Parameters.StepCount);
        Assert.Null(card.Parameters.Amount);
    }
}
