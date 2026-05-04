namespace MonoJoey.Server.GameEngine;

public enum CardActionKind
{
    Unspecified = 0,
    MoveToStart,
    MoveToTile,
    MoveRelative,
    MoveToNearestTransport,
    MoveToNearestUtility,
    ReceiveFromBank,
    PayBank,
    ReceiveFromEveryPlayer,
    PayEveryPlayer,
    RepairOwnedProperties,
    ApplySlimer,
    ApplyEarthquake,
    GoToLockup,
    HoldForLater
}
