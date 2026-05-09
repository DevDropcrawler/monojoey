namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record RaiseableCashResult(
    RaiseableCashResultKind ResultKind,
    PlayerId PlayerId,
    Money TotalRaiseableCash,
    IReadOnlyList<RaiseableCashItem> Items,
    string Message);

public enum RaiseableCashResultKind
{
    Calculated,
    PlayerNotInGame,
    PlayerBankrupt,
    PlayerEliminated,
    UnsafeMoneyTotal,
}

public sealed record RaiseableCashItem(
    RaiseableCashSource Source,
    TileId PropertyTileId,
    Money Amount,
    int UnitCount,
    int CurrentUpgradeLevel,
    bool RequiresUpgradeLiquidation);

public enum RaiseableCashSource
{
    UpgradeSale,
    Mortgage,
}
