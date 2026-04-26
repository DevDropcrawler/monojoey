namespace MonoJoey.Server.GameEngine;

public sealed record LockupEscapeUseResult(
    GameState GameState,
    LockupEscapeUseResultKind Kind);
