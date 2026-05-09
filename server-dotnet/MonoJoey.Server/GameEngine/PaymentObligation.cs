namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public sealed record PaymentObligation(
    PlayerId DebtorPlayerId,
    Money Amount,
    PaymentObligationKind Kind,
    PaymentObligationCreditor Creditor,
    TileId? TileId = null,
    CardId? CardId = null);

public enum PaymentObligationKind
{
    Rent,
    Tax,
    CardPayment,
    AuctionPayment,
    Fine,
    LoanInterest,
}

public readonly record struct PaymentObligationCreditor(
    PaymentObligationCreditorKind Kind,
    PlayerId? PlayerId)
{
    public static PaymentObligationCreditor Bank { get; } =
        new(PaymentObligationCreditorKind.Bank, PlayerId: null);

    public static PaymentObligationCreditor ForPlayer(PlayerId playerId)
    {
        return new PaymentObligationCreditor(PaymentObligationCreditorKind.Player, playerId);
    }
}

public enum PaymentObligationCreditorKind
{
    Bank,
    Player,
}
