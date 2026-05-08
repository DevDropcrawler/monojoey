namespace MonoJoey.Server.Tests.GameEngine;

using System.Text.Json;
using MonoJoey.Server.GameEngine;

public class GameRulesResolverTests
{
    [Fact]
    public void MonoJoeyDefaultPreset_ResolvesAllGroups()
    {
        using var document = JsonDocument.Parse(@"{""presetId"":""monojoey_default""}");

        var rules = GameRulesResolver.Resolve(document.RootElement);

        Assert.Equal(1, rules.Version);
        Assert.Equal("monojoey_default", rules.PresetId);
        Assert.Equal("MonoJoey default", rules.PresetName);
        Assert.False(rules.IsCustom);
        Assert.Equal(1500, rules.Economy.StartingMoney);
        Assert.Equal(200, rules.Economy.PassStartReward);
        Assert.Equal(100, rules.Economy.IncomeTaxAmount);
        Assert.Equal(100, rules.Economy.LuxuryTaxAmount);
        Assert.True(rules.Economy.BaseRentEnabled);
        Assert.False(rules.Economy.UpgradesEnabled);
        Assert.True(rules.Economy.MortgagesEnabled);
        Assert.Equal(50, rules.Economy.MortgageValuePercent);
        Assert.Equal(10, rules.Economy.UnmortgageInterestPercent);
        Assert.True(rules.Auction.MandatoryAuctionsEnabled);
        Assert.Equal(9, rules.Auction.InitialTimerSeconds);
        Assert.Equal(3, rules.Auction.BidResetTimerSeconds);
        Assert.Equal(1, rules.Auction.MinimumBidIncrement);
        Assert.Equal(0, rules.Auction.StartingBid);
        Assert.True(rules.Jail.Enabled);
        Assert.True(rules.Jail.EscapeCardsEnabled);
        Assert.Equal(50, rules.Jail.FineAmount);
        Assert.Equal(3, rules.Jail.MaxTurns);
        Assert.Equal(2, rules.Dice.DiceCount);
        Assert.Equal(6, rules.Dice.SidesPerDie);
        Assert.False(rules.Dice.DoublesExtraTurnEnabled);
        Assert.Equal(3, rules.Dice.MaxConsecutiveDoublesBeforeLockup);
        Assert.False(rules.Dice.ResolveLandingAfterCardMove);
        Assert.Equal(new[] { "chance", "table" }, rules.Cards.DecksEnabled);
        Assert.True(rules.Cards.IsDeckEnabled("chance"));
        Assert.True(rules.Cards.IsDeckEnabled("table"));
        Assert.True(rules.Cards.CustomCardsEnabled);
        Assert.True(rules.Cards.DeckEditingEnabled);
        Assert.Equal(CardDeckPresetIds.ClassicIsh, rules.Cards.DeckPresetId);
        Assert.True(rules.Loans.LoanSharkEnabled);
        Assert.Equal(0.20m, rules.Loans.BaseInterestRate);
        Assert.Equal(0.10m, rules.Loans.InterestRateIncreasePerLoan);
        Assert.Equal(0.20m, rules.Loans.InterestRateIncreasePerDebtTier);
        Assert.Equal(0, rules.Loans.MinimumInterestPayment);
        Assert.False(rules.Loans.CanBorrowForLoanPayments);
        Assert.Equal("lastPlayerStanding", rules.Win.ConditionType);
        Assert.False(rules.Future.SlimerEnabled);
        Assert.False(rules.Future.EarthquakeEnabled);
    }

    [Fact]
    public void PartialPayload_MergesOverDefaultAndMarksCustom()
    {
        using var document = JsonDocument.Parse(
            @"{
                ""presetName"": ""House rules"",
                ""economy"": {
                    ""passStartReward"": 125,
                    ""incomeTaxAmount"": 75,
                    ""luxuryTaxAmount"": 25,
                    ""mortgagesEnabled"": false,
                    ""mortgageValuePercent"": 40,
                    ""unmortgageInterestPercent"": 20
                },
                ""auction"": {
                    ""initialTimerSeconds"": 12,
                    ""minimumBidIncrement"": 5
                },
                ""loans"": {
                    ""loanSharkEnabled"": false
                }
            }");

        var rules = GameRulesResolver.Resolve(document.RootElement);

        Assert.Equal("custom", rules.PresetId);
        Assert.Equal("House rules", rules.PresetName);
        Assert.True(rules.IsCustom);
        Assert.Equal(12, rules.Auction.InitialTimerSeconds);
        Assert.Equal(3, rules.Auction.BidResetTimerSeconds);
        Assert.Equal(5, rules.Auction.MinimumBidIncrement);
        Assert.False(rules.Loans.LoanSharkEnabled);
        Assert.Equal(1500, rules.Economy.StartingMoney);
        Assert.Equal(125, rules.Economy.PassStartReward);
        Assert.Equal(75, rules.Economy.IncomeTaxAmount);
        Assert.Equal(25, rules.Economy.LuxuryTaxAmount);
        Assert.False(rules.Economy.MortgagesEnabled);
        Assert.Equal(40, rules.Economy.MortgageValuePercent);
        Assert.Equal(20, rules.Economy.UnmortgageInterestPercent);
        Assert.Equal(new[] { "chance", "table" }, rules.Cards.DecksEnabled);
    }

    [Fact]
    public void EmptyDecksEnabled_IsValidAndDisablesAllDeckChecks()
    {
        using var document = JsonDocument.Parse(@"{""cards"":{""decksEnabled"":[]}}");

        var rules = GameRulesResolver.Resolve(document.RootElement);

        Assert.Empty(rules.Cards.DecksEnabled);
        Assert.False(rules.Cards.IsDeckEnabled("chance"));
        Assert.False(rules.Cards.IsDeckEnabled("table"));
    }

    [Fact]
    public void CardRules_IsDeckEnabledMatchesOnlyConfiguredDeckIds()
    {
        using var document = JsonDocument.Parse(@"{""cards"":{""decksEnabled"":[""table""]}}");

        var rules = GameRulesResolver.Resolve(document.RootElement);

        Assert.Equal(new[] { "table" }, rules.Cards.DecksEnabled);
        Assert.False(rules.Cards.IsDeckEnabled("chance"));
        Assert.True(rules.Cards.IsDeckEnabled("table"));
        Assert.False(rules.Cards.IsDeckEnabled("TABLE"));
        Assert.False(rules.Cards.IsDeckEnabled("missing"));
    }

    [Theory]
    [InlineData("classic_ish")]
    [InlineData("chaos")]
    [InlineData("custom_ready")]
    public void CardRules_AcceptsKnownDeckPresetIds(string deckPresetId)
    {
        using var document = JsonDocument.Parse($@"{{""cards"":{{""deckPresetId"":""{deckPresetId}""}}}}");

        var rules = GameRulesResolver.Resolve(document.RootElement);

        Assert.Equal(deckPresetId, rules.Cards.DeckPresetId);
    }

    [Fact]
    public void CardRules_DefaultConstructionUsesClassicIshDeckPreset()
    {
        var rules = new CardRules(
            new[] { CardDeckIds.Chance, CardDeckIds.Table },
            customCardsEnabled: true,
            deckEditingEnabled: true);

        Assert.Equal(CardDeckPresetIds.ClassicIsh, rules.DeckPresetId);
    }

    [Fact]
    public void JailPayload_ResolvesCustomSettings()
    {
        using var document = JsonDocument.Parse(
            @"{
                ""jail"": {
                    ""enabled"": false,
                    ""escapeCardsEnabled"": false,
                    ""fineAmount"": 75,
                    ""maxTurns"": 4
                }
            }");

        var rules = GameRulesResolver.Resolve(document.RootElement);

        Assert.Equal("custom", rules.PresetId);
        Assert.False(rules.Jail.Enabled);
        Assert.False(rules.Jail.EscapeCardsEnabled);
        Assert.Equal(75, rules.Jail.FineAmount);
        Assert.Equal(4, rules.Jail.MaxTurns);
    }

    [Fact]
    public void DicePayload_ResolvesCustomDoubleSettings()
    {
        using var document = JsonDocument.Parse(
            @"{
                ""dice"": {
                    ""sidesPerDie"": 8,
                    ""doublesExtraTurnEnabled"": true,
                    ""maxConsecutiveDoublesBeforeLockup"": 2
                }
            }");

        var rules = GameRulesResolver.Resolve(document.RootElement);

        Assert.Equal("custom", rules.PresetId);
        Assert.Equal(8, rules.Dice.SidesPerDie);
        Assert.True(rules.Dice.DoublesExtraTurnEnabled);
        Assert.Equal(2, rules.Dice.MaxConsecutiveDoublesBeforeLockup);
    }

    [Fact]
    public void TrustedMetadata_IsDerivedByServer()
    {
        using var document = JsonDocument.Parse(@"{""presetId"":""custom"",""isCustom"":true}");

        var rules = GameRulesResolver.Resolve(document.RootElement);

        Assert.Equal("monojoey_default", rules.PresetId);
        Assert.False(rules.IsCustom);
        Assert.Equal("MonoJoey default", rules.PresetName);
    }

    [Theory]
    [InlineData(@"{""presetId"":""unknown""}")]
    [InlineData(@"{""unknownGroup"":{}}")]
    [InlineData(@"{""auction"":{""unknownField"":1}}")]
    [InlineData(@"{""auction"":{""initialTimerSeconds"":0}}")]
    [InlineData(@"{""auction"":{""minimumBidIncrement"":""5""}}")]
    [InlineData(@"{""economy"":{""incomeTaxAmount"":-1}}")]
    [InlineData(@"{""economy"":{""luxuryTaxAmount"":-1}}")]
    [InlineData(@"{""economy"":{""mortgagesEnabled"":""yes""}}")]
    [InlineData(@"{""economy"":{""mortgageValuePercent"":-1}}")]
    [InlineData(@"{""economy"":{""mortgageValuePercent"":101}}")]
    [InlineData(@"{""economy"":{""mortgageValuePercent"":50.5}}")]
    [InlineData(@"{""economy"":{""unmortgageInterestPercent"":-1}}")]
    [InlineData(@"{""economy"":{""unmortgageInterestPercent"":101}}")]
    [InlineData(@"{""economy"":{""unmortgageInterestPercent"":10.5}}")]
    [InlineData(@"{""jail"":{""enabled"":""yes""}}")]
    [InlineData(@"{""jail"":{""escapeCardsEnabled"":""yes""}}")]
    [InlineData(@"{""jail"":{""fineAmount"":-1}}")]
    [InlineData(@"{""jail"":{""fineAmount"":50.5}}")]
    [InlineData(@"{""jail"":{""maxTurns"":0}}")]
    [InlineData(@"{""jail"":{""maxTurns"":3.5}}")]
    [InlineData(@"{""dice"":{""diceCount"":0}}")]
    [InlineData(@"{""dice"":{""sidesPerDie"":1}}")]
    [InlineData(@"{""dice"":{""doublesExtraTurnEnabled"":""yes""}}")]
    [InlineData(@"{""dice"":{""maxConsecutiveDoublesBeforeLockup"":0}}")]
    [InlineData(@"{""dice"":{""maxConsecutiveDoublesBeforeLockup"":""3""}}")]
    [InlineData(@"{""loans"":{""baseInterestRate"":1.5}}")]
    [InlineData(@"{""cards"":{""decksEnabled"":[""chance"",""missing""]}}")]
    [InlineData(@"{""cards"":{""deckPresetId"":""missing""}}")]
    [InlineData(@"{""cards"":{""deckPresetId"":""""}}")]
    [InlineData(@"{""cards"":{""deckPresetId"":null}}")]
    [InlineData(@"{""win"":{""conditionType"":""score""}}")]
    [InlineData(@"{""future"":{""slimerEnabled"":""yes""}}")]
    public void InvalidPayloads_AreRejected(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Throws<GameRulesValidationException>(() => GameRulesResolver.Resolve(document.RootElement));
    }
}
