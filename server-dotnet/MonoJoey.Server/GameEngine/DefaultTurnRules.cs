namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class DefaultTurnRules
{
    public static readonly Money PassStartReward = new(200);

    public static readonly Money TaxAmount = new(100);
}
