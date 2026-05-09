namespace MonoJoey.Server.GameEngine;

public static class EconomyRulesCalculator
{
    public static Money CalculateUpgradeSellRefund(Money upgradeCost, EconomyRules rules)
    {
        return new Money((int)Math.Floor(upgradeCost.Amount * rules.UpgradeSellRefundPercent / 100m));
    }
}
