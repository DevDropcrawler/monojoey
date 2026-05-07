namespace MonoJoey.Server.GameEngine.Stats;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;

internal sealed record StatEvent(
    PlayerId PlayerId,
    StatEventKind Kind,
    Money? Amount = null,
    TileId? TileId = null,
    string? Source = null);
