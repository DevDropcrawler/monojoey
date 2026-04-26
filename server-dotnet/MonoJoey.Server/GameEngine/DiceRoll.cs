namespace MonoJoey.Server.GameEngine;

public sealed record DiceRoll
{
    public const int MinFaceValue = 1;
    public const int MaxFaceValue = 6;

    public DiceRoll(int firstDie, int secondDie)
    {
        ValidateDie(nameof(firstDie), firstDie);
        ValidateDie(nameof(secondDie), secondDie);

        FirstDie = firstDie;
        SecondDie = secondDie;
    }

    public int FirstDie { get; }

    public int SecondDie { get; }

    public int Total => FirstDie + SecondDie;

    public bool IsDouble => FirstDie == SecondDie;

    private static void ValidateDie(string parameterName, int value)
    {
        if (value is < MinFaceValue or > MaxFaceValue)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Dice values must be between {MinFaceValue} and {MaxFaceValue}.");
        }
    }
}
