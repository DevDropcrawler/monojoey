namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record MovementResult(
    GameState GameState,
    PlayerId PlayerId,
    TileId LandingTileId,
    int LandingTileIndex,
    bool PassedStart);
