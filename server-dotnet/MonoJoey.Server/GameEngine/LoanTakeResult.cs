namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record LoanTakeResult(
    LoanTakeResultKind ResultKind,
    GameState GameState,
    PlayerId PlayerId,
    Money Amount,
    BorrowPurpose Purpose,
    PlayerLoanState? LoanState,
    string Message)
{
    public bool LoanTaken => ResultKind == LoanTakeResultKind.Accepted;
}

public enum LoanTakeResultKind
{
    Accepted = 0,
    PlayerNotInGame,
    PlayerEliminated,
    InvalidAmount,
    DisallowedBorrowPurpose,
}
