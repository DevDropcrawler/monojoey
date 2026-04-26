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

    public DiceRoll Roll()
    {
        return new DiceRoll(RollDie(), RollDie());
    }

    private int RollDie()
    {
        return random.Next(DiceRoll.MinFaceValue, DiceRoll.MaxFaceValue + 1);
    }
}
