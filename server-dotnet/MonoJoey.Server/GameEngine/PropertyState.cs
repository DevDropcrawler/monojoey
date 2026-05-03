namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record PropertyState(
    TileId TileId,
    PropertyStateData Data);

public sealed record PropertyStateData;
