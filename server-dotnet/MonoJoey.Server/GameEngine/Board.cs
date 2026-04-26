namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record Board(
    BoardId BoardId,
    int Version,
    string DisplayName,
    IReadOnlyList<Tile> Tiles);
