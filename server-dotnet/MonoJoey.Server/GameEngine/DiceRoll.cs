namespace MonoJoey.Server.GameEngine;

public sealed record DiceRoll
{
    public const int MinFaceValue = 1;
    public const int MaxFaceValue = 6;

    public DiceRoll(int firstDie, int secondDie)
        : this(firstDie, secondDie, MaxFaceValue)
    {
    }

    public DiceRoll(int firstDie, int secondDie, int sidesPerDie)
    {
        ValidateSidesPerDie(sidesPerDie);
        ValidateDie(nameof(firstDie), firstDie, sidesPerDie);
        ValidateDie(nameof(secondDie), secondDie, sidesPerDie);

        FirstDie = firstDie;
        SecondDie = secondDie;
    }

    public int FirstDie { get; }

    public int SecondDie { get; }

    public int Total => FirstDie + SecondDie;

    public bool IsDouble => FirstDie == SecondDie;

    private static void ValidateSidesPerDie(int sidesPerDie)
    {
        if (sidesPerDie < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sidesPerDie),
                sidesPerDie,
                "Dice must have at least 2 sides.");
        }
    }

    private static void ValidateDie(string parameterName, int value, int sidesPerDie)
    {
        if (value < MinFaceValue || value > sidesPerDie)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Dice values must be between {MinFaceValue} and {sidesPerDie}.");
        }
    }
}
