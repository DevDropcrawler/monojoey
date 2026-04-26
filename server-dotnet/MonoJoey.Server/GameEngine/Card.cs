namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record Card(
    CardId CardId,
    string DisplayName,
    CardActionKind ActionKind,
    CardActionParameters? Parameters = null);
