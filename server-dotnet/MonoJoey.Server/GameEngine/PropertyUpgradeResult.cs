namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record PropertyUpgradeResult(
    PropertyUpgradeResultKind ResultKind,
    GameState GameState,
    PlayerId PlayerId,
    TileId PropertyTileId,
    int UpgradeLevel,
    Money UpgradeCost,
    Money RefundAmount,
    Money Money,
    string Message)
{
    public bool UpgradeBought => ResultKind == PropertyUpgradeResultKind.UpgradeBought;

    public bool UpgradeSold => ResultKind == PropertyUpgradeResultKind.UpgradeSold;
}

public enum PropertyUpgradeResultKind
{
    UpgradeBought,
    UpgradeSold,
    UpgradesDisabled,
    PlayerNotInGame,
    PlayerBankrupt,
    PlayerEliminated,
    GameNotInProgress,
    ActiveAuction,
    UnresolvedTileExecution,
    InvalidProperty,
    PropertyNotOwned,
    IncompleteGroupOwnership,
    MortgagedGroupProperty,
    MaximumUpgradeLevel,
    MinimumUpgradeLevel,
    RentTierUnavailable,
    UnevenUpgrade,
    UnevenDowngrade,
    InsufficientCash,
    UnsafeMoneyBalance,
}
