namespace MonoJoey.Server.GameEngine;

public sealed record SolvencyAnalysisResult(
    SolvencyAnalysisResultKind ResultKind,
    PaymentObligation Obligation,
    Money CashOnHand,
    RaiseableCashResult RaiseableCash,
    Money TotalAvailable,
    Money Shortfall,
    IReadOnlyList<RaiseableCashItem> RaiseableCashItems,
    string Message);

public enum SolvencyAnalysisResultKind
{
    SolventWithCash,
    SolventWithAssets,
    Insolvent,
    InvalidObligation,
    PlayerNotInGame,
    PlayerBankrupt,
    PlayerEliminated,
    UnsafeMoneyTotal,
}
