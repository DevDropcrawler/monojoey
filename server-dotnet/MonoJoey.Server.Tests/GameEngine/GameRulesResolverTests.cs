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
        Assert.True(rules.Auction.MandatoryAuctionsEnabled);
        Assert.Equal(9, rules.Auction.InitialTimerSeconds);
        Assert.Equal(3, rules.Auction.BidResetTimerSeconds);
        Assert.Equal(1, rules.Auction.MinimumBidIncrement);
        Assert.Equal(0, rules.Auction.StartingBid);
        Assert.True(rules.Jail.Enabled);
        Assert.True(rules.Jail.EscapeCardsEnabled);
        Assert.Equal(2, rules.Dice.DiceCount);
        Assert.Equal(6, rules.Dice.SidesPerDie);
        Assert.False(rules.Dice.ResolveLandingAfterCardMove);
        Assert.Equal(new[] { "chance", "table" }, rules.Cards.DecksEnabled);
        Assert.True(rules.Cards.CustomCardsEnabled);
        Assert.True(rules.Cards.DeckEditingEnabled);
        Assert.True(rules.Loans.LoanSharkEnabled);
        Assert.Equal(0.25m, rules.Loans.BaseInterestRate);
        Assert.Equal(0.10m, rules.Loans.InterestRateIncreasePerLoan);
        Assert.Equal(0.05m, rules.Loans.InterestRateIncreasePerDebtTier);
        Assert.Equal(25, rules.Loans.MinimumInterestPayment);
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
                    ""luxuryTaxAmount"": 25
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
        Assert.Equal(new[] { "chance", "table" }, rules.Cards.DecksEnabled);
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
    [InlineData(@"{""dice"":{""diceCount"":0}}")]
    [InlineData(@"{""dice"":{""sidesPerDie"":1}}")]
    [InlineData(@"{""loans"":{""baseInterestRate"":1.5}}")]
    [InlineData(@"{""cards"":{""decksEnabled"":[""chance"",""missing""]}}")]
    [InlineData(@"{""win"":{""conditionType"":""score""}}")]
    [InlineData(@"{""future"":{""slimerEnabled"":""yes""}}")]
    public void InvalidPayloads_AreRejected(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Throws<GameRulesValidationException>(() => GameRulesResolver.Resolve(document.RootElement));
    }
}
