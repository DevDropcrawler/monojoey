namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record PropertyState(
    TileId TileId,
    PropertyStateData Data);

public sealed record PropertyStateData
{
    public PropertyStateData(int damagePercent = 0, bool isMortgaged = false)
    {
        if (damagePercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(damagePercent),
                "Damage percent must be between 0 and 100.");
        }

        DamagePercent = damagePercent;
        IsMortgaged = isMortgaged;
    }

    public int DamagePercent { get; }

    public bool IsMortgaged { get; }
}
