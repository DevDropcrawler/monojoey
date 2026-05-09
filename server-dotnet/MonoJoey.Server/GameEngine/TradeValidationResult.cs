namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record TradeValidationResult(
    TradeSettlementResultKind ResultKind,
    PlayerId FirstPlayerId,
    PlayerId SecondPlayerId,
    TradeAssets FirstPlayerAssets,
    TradeAssets SecondPlayerAssets,
    string Message)
{
    public bool TradeValid => ResultKind == TradeSettlementResultKind.Settled;
}
