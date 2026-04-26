namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class CardResolver
{
    private static readonly TileId StartTileId = new("start");

    public static CardResolutionResult ResolveCard(Player player, Card card)
    {
        return card.ActionKind switch
        {
            CardActionKind.MoveToStart => Resolved(
                player,
                card,
                CardResolutionActionKind.MoveToStart,
                new CardActionParameters(TargetTileId: StartTileId)),

            CardActionKind.MoveToTile when card.Parameters?.TargetTileId is not null => Resolved(
                player,
                card,
                CardResolutionActionKind.MoveToTile,
                card.Parameters),

            CardActionKind.MoveRelative when card.Parameters?.StepCount is not null => Resolved(
                player,
                card,
                CardResolutionActionKind.MoveSteps,
                card.Parameters),

            CardActionKind.MoveToNearestTransport => Resolved(
                player,
                card,
                CardResolutionActionKind.MoveToNearestTransport,
                parameters: null),

            CardActionKind.MoveToNearestUtility => Resolved(
                player,
                card,
                CardResolutionActionKind.MoveToNearestUtility,
                parameters: null),

            CardActionKind.ReceiveFromBank when card.Parameters?.Amount is not null => Resolved(
                player,
                card,
                CardResolutionActionKind.ReceiveMoney,
                card.Parameters),

            CardActionKind.PayBank when card.Parameters?.Amount is not null => Resolved(
                player,
                card,
                CardResolutionActionKind.PayMoney,
                card.Parameters),

            CardActionKind.ReceiveFromEveryPlayer when card.Parameters?.Amount is not null => Resolved(
                player,
                card,
                CardResolutionActionKind.ReceiveMoneyFromEveryPlayer,
                card.Parameters),

            CardActionKind.PayEveryPlayer when card.Parameters?.Amount is not null => Resolved(
                player,
                card,
                CardResolutionActionKind.PayMoneyToEveryPlayer,
                card.Parameters),

            CardActionKind.RepairOwnedProperties when card.Parameters?.Amount is not null => Resolved(
                player,
                card,
                CardResolutionActionKind.RepairOwnedProperties,
                card.Parameters),

            CardActionKind.GoToLockup => Resolved(
                player,
                card,
                CardResolutionActionKind.GoToLockup,
                parameters: null),

            CardActionKind.HoldForLater => Resolved(
                player,
                card,
                CardResolutionActionKind.GetOutOfLockup,
                parameters: null),

            _ => Invalid(player, card),
        };
    }

    private static CardResolutionResult Resolved(
        Player player,
        Card card,
        CardResolutionActionKind actionKind,
        CardActionParameters? parameters)
    {
        return new CardResolutionResult(player.PlayerId, card.CardId, actionKind, parameters);
    }

    private static CardResolutionResult Invalid(Player player, Card card)
    {
        return new CardResolutionResult(
            player.PlayerId,
            card.CardId,
            CardResolutionActionKind.InvalidCard,
            Parameters: null);
    }
}
