namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;

public class DiceServiceTests
{
    [Fact]
    public void RollDice_UsesInjectedRoller()
    {
        var diceService = new DiceService(new FixedDiceRoller(new DiceRoll(2, 5)));

        var roll = diceService.RollDice();

        Assert.Equal(2, roll.FirstDie);
        Assert.Equal(5, roll.SecondDie);
        Assert.Equal(7, roll.Total);
        Assert.False(roll.IsDouble);
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
    public void RandomDiceRoller_ReturnsValuesInsideStandardRange()
    {
        var diceRoller = new RandomDiceRoller(new Random(12345));

        for (var index = 0; index < 100; index++)
        {
            var roll = diceRoller.Roll();

            Assert.InRange(roll.FirstDie, DiceRoll.MinFaceValue, DiceRoll.MaxFaceValue);
            Assert.InRange(roll.SecondDie, DiceRoll.MinFaceValue, DiceRoll.MaxFaceValue);
            Assert.InRange(roll.Total, DiceRoll.MinFaceValue * 2, DiceRoll.MaxFaceValue * 2);
        }
    }

    private sealed class FixedDiceRoller : IDiceRoller
    {
        private readonly DiceRoll roll;

        public FixedDiceRoller(DiceRoll roll)
        {
            this.roll = roll;
        }

        public DiceRoll Roll()
        {
            return roll;
        }
    }
}
