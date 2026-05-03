namespace MonoJoey.Server.GameEngine;

public sealed record PlayerStatusEffectData(
    string DefinitionId,
    int StackCount = 1,
    int? RemainingTurns = null,
    string? SourceId = null);
