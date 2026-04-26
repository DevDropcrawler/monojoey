namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record PlayerEliminationResult(
    GameState GameState,
    PlayerId PlayerId,
    EliminationReason Reason,
    Money Balance,
    Money? PaymentDue,
    Money? PaymentAvailable,
    bool WasEliminated);
