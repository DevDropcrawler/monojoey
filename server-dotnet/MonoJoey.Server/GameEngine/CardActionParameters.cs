namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record CardActionParameters(
    TileId? TargetTileId = null,
    int? StepCount = null,
    Money? Amount = null);
