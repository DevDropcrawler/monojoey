namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record PlayerCashTransferResult(
    PlayerCashTransferResultKind ResultKind,
    GameState GameState,
    PlayerId FromPlayerId,
    PlayerId ToPlayerId,
    Money Amount,
    Money FromPlayerBalance,
    Money ToPlayerBalance,
    string Message)
{
    public bool TransferAccepted => ResultKind == PlayerCashTransferResultKind.Transferred;
}

public enum PlayerCashTransferResultKind
{
    Transferred,
    SamePlayer,
    PlayerNotInGame,
    PlayerBankrupt,
    PlayerEliminated,
    InvalidAmount,
    InsufficientCash,
    UnsafeMoneyBalance,
}
