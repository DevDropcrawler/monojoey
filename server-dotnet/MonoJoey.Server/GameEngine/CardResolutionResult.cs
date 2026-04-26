namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record CardResolutionResult(
    PlayerId PlayerId,
    CardId CardId,
    CardResolutionActionKind ActionKind,
    CardActionParameters? Parameters)
{
    public bool IsValid => ActionKind != CardResolutionActionKind.InvalidCard;
}
