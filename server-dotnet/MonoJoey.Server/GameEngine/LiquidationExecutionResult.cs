namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record LiquidationExecutionResult(
    LiquidationExecutionResultKind ResultKind,
    GameState GameState,
    PaymentObligation Obligation,
    IReadOnlyList<LiquidationStepResult> Steps,
    Money AmountPaid,
    Money DebtorBalance,
    Money? CreditorBalance,
    string Message)
{
    public bool PaymentExecuted => ResultKind == LiquidationExecutionResultKind.PaymentExecuted;
}

public enum LiquidationExecutionResultKind
{
    PaymentExecuted,
    InvalidObligation,
    GameNotInProgress,
    ActiveAuction,
    UnresolvedTileExecution,
    DebtorNotInGame,
    DebtorBankrupt,
    DebtorEliminated,
    CreditorNotInGame,
    CreditorBankrupt,
    CreditorEliminated,
    Insolvent,
    UnsafeMoneyBalance,
    UnsupportedPayment,
}

public sealed record LiquidationStepResult(
    LiquidationStepKind StepKind,
    TileId? PropertyTileId,
    Money Amount,
    Money DebtorBalance,
    int? UpgradeLevel,
    bool? IsMortgaged);

public enum LiquidationStepKind
{
    UpgradeSale,
    Mortgage,
    BankPayment,
    PlayerPayment,
}
