namespace MonoJoey.Server.GameEngine;

public enum CardResolutionActionKind
{
    InvalidCard = 0,
    MoveToStart,
    MoveToTile,
    MoveSteps,
    MoveToNearestTransport,
    MoveToNearestUtility,
    ReceiveMoney,
    PayMoney,
    ReceiveMoneyFromEveryPlayer,
    PayMoneyToEveryPlayer,
    RepairOwnedProperties,
    ApplySlimer,
    ApplyEarthquake,
    GoToLockup,
    GetOutOfLockup
}
