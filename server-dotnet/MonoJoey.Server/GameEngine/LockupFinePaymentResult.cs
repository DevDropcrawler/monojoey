namespace MonoJoey.Server.GameEngine;

public sealed record LockupFinePaymentResult(
    GameState GameState,
    LockupFinePaymentResultKind Kind,
    Money FineAmount);
