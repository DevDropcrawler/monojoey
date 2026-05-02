namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record MovementResult(
    GameState GameState,
    PlayerId PlayerId,
    TileId FromTileId,
    TileId ToTileId,
    TileId LandingTileId,
    int LandingTileIndex,
    IReadOnlyList<TileId> PathTileIds,
    int StepCount,
    string MovementKind,
    bool PassedStart);
