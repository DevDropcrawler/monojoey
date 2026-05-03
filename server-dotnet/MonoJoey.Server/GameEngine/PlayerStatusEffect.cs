namespace MonoJoey.Server.GameEngine;

public sealed record PlayerStatusEffect(
    string? InstanceId,
    PlayerStatusEffectKind Kind,
    PlayerStatusEffectData Data);
