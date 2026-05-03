namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;

public class DiceServiceTests
{
    [Fact]
    public void RollDice_UsesInjectedRoller()
    {
        var diceService = new DiceService(new FixedDiceRoller(new DiceRoll(2, 5)));

        var roll = diceService.RollDice(DiceRoll.MaxFaceValue);

        Assert.Equal(2, roll.FirstDie);
        Assert.Equal(5, roll.SecondDie);
        Assert.Equal(7, roll.Total);
        Assert.False(roll.IsDouble);
    }

    [Fact]
    public void RollDice_PassesSidesToInjectedRoller()
    {
        var diceRoller = new FixedDiceRoller(new DiceRoll(7, 8, 8));
        var diceService = new DiceService(diceRoller);

        var roll = diceService.RollDice(8);

        Assert.Equal(8, diceRoller.LastSidesPerDie);
        Assert.Equal(15, roll.Total);
    }

    [Fact]
    public void DiceRoll_ReportsDoubles()
    {
        var roll = new DiceRoll(4, 4);

        Assert.Equal(8, roll.Total);
        Assert.True(roll.IsDouble);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(7, 1)]
    [InlineData(1, 7)]
    public void DiceRoll_RejectsValuesOutsideStandardRange(int firstDie, int secondDie)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiceRoll(firstDie, secondDie));
    }

    [Fact]
    public void DiceRoll_CustomSidesValidatesAgainstProvidedRange()
    {
        var roll = new DiceRoll(7, 8, 8);

        Assert.Equal(7, roll.FirstDie);
        Assert.Equal(8, roll.SecondDie);
        Assert.Equal(15, roll.Total);
        Assert.False(roll.IsDouble);
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiceRoll(9, 1, 8));
    }

    [Fact]
    public void RandomDiceRoller_ReturnsValuesInsideStandardRange()
    {
        var diceRoller = new RandomDiceRoller(new Random(12345));

        for (var index = 0; index < 100; index++)
        {
            var roll = diceRoller.Roll(DiceRoll.MaxFaceValue);

            Assert.InRange(roll.FirstDie, DiceRoll.MinFaceValue, DiceRoll.MaxFaceValue);
            Assert.InRange(roll.SecondDie, DiceRoll.MinFaceValue, DiceRoll.MaxFaceValue);
            Assert.InRange(roll.Total, DiceRoll.MinFaceValue * 2, DiceRoll.MaxFaceValue * 2);
        }
    }

    [Fact]
    public void RandomDiceRoller_ReturnsTwoValuesInsideConfiguredSides()
    {
        var diceRoller = new RandomDiceRoller(new Random(12345));

        for (var index = 0; index < 100; index++)
        {
            var roll = diceRoller.Roll(8);

            Assert.InRange(roll.FirstDie, DiceRoll.MinFaceValue, 8);
            Assert.InRange(roll.SecondDie, DiceRoll.MinFaceValue, 8);
            Assert.InRange(roll.Total, DiceRoll.MinFaceValue * 2, 8 * 2);
        }
    }

    private sealed class FixedDiceRoller : IDiceRoller
    {
        private readonly DiceRoll roll;

        public FixedDiceRoller(DiceRoll roll)
        {
            this.roll = roll;
        }

        public int LastSidesPerDie { get; private set; }

        public DiceRoll Roll(int sidesPerDie)
        {
            LastSidesPerDie = sidesPerDie;

            return roll;
        }
    }
}
