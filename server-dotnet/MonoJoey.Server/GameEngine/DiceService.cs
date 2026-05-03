namespace MonoJoey.Server.GameEngine;

public sealed class DiceService
{
    private readonly IDiceRoller diceRoller;

    public DiceService(IDiceRoller diceRoller)
    {
        this.diceRoller = diceRoller;
    }

    public DiceRoll RollDice(int sidesPerDie)
    {
        return diceRoller.Roll(sidesPerDie);
    }
}
