namespace MonoJoey.Server.GameEngine;

public readonly record struct Money(int Amount)
{
    public static Money Zero { get; } = new(0);
}
