namespace MonoJoey.Server.GameEngine;

public static class EconomyRulesCalculator
{
    public static Money CalculateMortgageValue(Money price, EconomyRules rules)
    {
        return new Money((int)Math.Floor(price.Amount * rules.MortgageValuePercent / 100m));
    }

    public static Money CalculateUnmortgageInterest(Money mortgageValue, EconomyRules rules)
    {
        return new Money((int)Math.Floor(mortgageValue.Amount * rules.UnmortgageInterestPercent / 100m));
    }

    public static Money CalculateUpgradeSellRefund(Money upgradeCost, EconomyRules rules)
    {
        return new Money((int)Math.Floor(upgradeCost.Amount * rules.UpgradeSellRefundPercent / 100m));
    }
}
