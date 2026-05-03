namespace MonoJoey.Server.GameEngine;

public sealed class RandomDiceRoller : IDiceRoller
{
    private readonly Random random;

    public RandomDiceRoller()
        : this(Random.Shared)
    {
    }

    public RandomDiceRoller(Random random)
    {
        this.random = random;
    }

    public DiceRoll Roll(int sidesPerDie)
    {
        return new DiceRoll(RollDie(sidesPerDie), RollDie(sidesPerDie), sidesPerDie);
    }

    private int RollDie(int sidesPerDie)
    {
        return random.Next(DiceRoll.MinFaceValue, sidesPerDie + 1);
    }
}
