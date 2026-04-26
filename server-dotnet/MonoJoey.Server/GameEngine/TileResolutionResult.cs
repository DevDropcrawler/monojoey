namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public sealed record TileResolutionResult(
    PlayerId PlayerId,
    TileId TileId,
    int TileIndex,
    TileType TileType,
    bool RequiresAction,
    TileResolutionActionKind ActionKind)
{
    public bool NoAction => !RequiresAction;
}
