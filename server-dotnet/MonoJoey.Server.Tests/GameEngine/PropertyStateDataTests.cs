namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;

public class PropertyStateDataTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Constructor_AcceptsValidUpgradeLevels(int upgradeLevel)
    {
        var data = new PropertyStateData(upgradeLevel: upgradeLevel);

        Assert.Equal(upgradeLevel, data.UpgradeLevel);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void Constructor_RejectsUpgradeLevelsOutsideValidRange(int upgradeLevel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PropertyStateData(upgradeLevel: upgradeLevel));
    }
}
