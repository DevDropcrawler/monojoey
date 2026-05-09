namespace MonoJoey.Server.Sessions;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;

public sealed record PendingTradeOffer(
    string TradeOfferId,
    long CreatedSequence,
    PlayerId ProposerPlayerId,
    PlayerId RecipientPlayerId,
    TradeAssets Offered,
    TradeAssets Requested,
    DateTimeOffset CreatedAtUtc);
