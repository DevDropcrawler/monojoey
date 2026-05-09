namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;

public class EconomyRulesCalculatorTests
{
    [Fact]
    public void CalculateMortgageValue_UsesConfiguredPercentAndFloors()
    {
        var rules = GameRulesPresets.MonoJoeyDefault.Economy with { MortgageValuePercent = 33 };

        var result = EconomyRulesCalculator.CalculateMortgageValue(new Money(101), rules);

        Assert.Equal(new Money(33), result);
    }

    [Fact]
    public void CalculateUnmortgageInterest_UsesConfiguredPercentAndFloors()
    {
        var rules = GameRulesPresets.MonoJoeyDefault.Economy with { UnmortgageInterestPercent = 12 };

        var result = EconomyRulesCalculator.CalculateUnmortgageInterest(new Money(99), rules);

        Assert.Equal(new Money(11), result);
    }

    [Fact]
    public void CalculateUpgradeSellRefund_UsesConfiguredPercentAndFloors()
    {
        var rules = GameRulesPresets.MonoJoeyDefault.Economy with { UpgradeSellRefundPercent = 25 };

        var result = EconomyRulesCalculator.CalculateUpgradeSellRefund(new Money(50), rules);

        Assert.Equal(new Money(12), result);
    }
}
