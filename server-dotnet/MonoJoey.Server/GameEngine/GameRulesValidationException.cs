namespace MonoJoey.Server.GameEngine;

public sealed class GameRulesValidationException : Exception
{
    public GameRulesValidationException(string message)
        : base(message)
    {
    }
}
