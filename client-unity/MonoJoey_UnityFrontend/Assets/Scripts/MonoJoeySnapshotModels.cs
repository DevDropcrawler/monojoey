using System;

[Serializable]
public sealed class MonoJoeySnapshotResultEnvelope
{
    public string type;
    public MonoJoeySnapshot payload;
}

[Serializable]
public sealed class MonoJoeyReconnectResultEnvelope
{
    public string type;
    public MonoJoeyReconnectPayload payload;
}

[Serializable]
public sealed class MonoJoeyReconnectPayload
{
    public string sessionId;
    public string playerId;
    public long lastEventSequence;
    public MonoJoeySnapshot snapshot;
}

[Serializable]
public sealed class MonoJoeySnapshot
{
    public int snapshotVersion;
    public string sessionId;
    public string status;
    public string gameStatus;
    public string serverNowUtc;
    public string matchId;
    public string phase;
    public string winnerPlayerId;
    public string startedAtUtc;
    public string endedAtUtc;
    public MonoJoeyTurnSnapshot turn;
    public MonoJoeyPlayerSnapshot[] players;
    public MonoJoeyBoardSnapshot board;
    public MonoJoeyPropertyStateSnapshot[] propertyStates;
    public MonoJoeyActiveAuctionSnapshot activeAuction;
    public MonoJoeyMovementPayload movement;
    public MonoJoeyMoneyDeltaPayload[] moneyDeltas;
    public MonoJoeyPropertyOwnershipChangePayload[] propertyOwnershipChanges;
    public MonoJoeyPlayerEliminationPayload[] playerEliminations;
}

[Serializable]
public sealed class MonoJoeyTurnSnapshot
{
    public string currentPlayerId;
    public int turnIndex;
    public bool hasRolledThisTurn;
    public bool hasResolvedTileThisTurn;
    public bool hasExecutedTileThisTurn;
}

[Serializable]
public sealed class MonoJoeyPlayerSnapshot
{
    public string playerId;
    public string username;
    public string tokenId;
    public string colorId;
    public int money;
    public string currentTileId;
    public string[] ownedPropertyIds;
    public string[] heldCardIds;
    public MonoJoeyStatusEffectSnapshot[] statusEffects;
    public MonoJoeyLoanSnapshot loan;
    public int jailTurnCount;
    public int jailRollAttemptCount;
    public int consecutiveDoublesCount;
    public string lastJailReleaseReason;
    public bool isBankrupt;
    public bool isEliminated;
    public bool isLockedUp;
}

[Serializable]
public sealed class MonoJoeyStatusEffectSnapshot
{
    public string instanceId;
    public string kind;
    public MonoJoeyStatusEffectDataSnapshot data;
}

[Serializable]
public sealed class MonoJoeyStatusEffectDataSnapshot
{
    public string definitionId;
    public int stackCount;
    public int remainingTurns;
    public string sourceId;
}

[Serializable]
public sealed class MonoJoeyLoanSnapshot
{
    public int totalBorrowed;
    public float currentInterestRatePercent;
    public int nextTurnInterestDue;
    public int loanTier;
}

[Serializable]
public sealed class MonoJoeyBoardSnapshot
{
    public string boardId;
    public int version;
    public string displayName;
    public MonoJoeyBoardTileSnapshot[] tiles;
}

[Serializable]
public sealed class MonoJoeyBoardTileSnapshot
{
    public string tileId;
    public int index;
    public string displayName;
    public string tileType;
    public string groupId;
    public int price;
    public int[] rentTable;
    public int upgradeCost;
    public bool isPurchasable;
    public bool isAuctionable;
    public string ownerPlayerId;
}

[Serializable]
public sealed class MonoJoeyPropertyStateSnapshot
{
    public string tileId;
    public MonoJoeyPropertyStateDataSnapshot data;
}

[Serializable]
public sealed class MonoJoeyPropertyStateDataSnapshot
{
    public int damagePercent;
    public bool isMortgaged;
    public int upgradeLevel;
}

[Serializable]
public sealed class MonoJoeyActiveAuctionSnapshot
{
    public string propertyTileId;
    public string triggeringPlayerId;
    public string status;
    public int startingBid;
    public int minimumBidIncrement;
    public int initialPreBidSeconds;
    public int bidResetSeconds;
    public int highestBid;
    public string highestBidderId;
    public float countdownDurationSeconds;
    public string timerEndsAtUtc;
    public MonoJoeyAuctionBidSnapshot[] bids;
}

[Serializable]
public sealed class MonoJoeyAuctionBidSnapshot
{
    public string bidderPlayerId;
    public int amount;
    public string placedAtUtc;
}

[Serializable]
public sealed class MonoJoeyMovementPayload
{
    public string playerId;
    public string fromTileId;
    public string toTileId;
    public string[] pathTileIds;
    public int stepCount;
    public string movementKind;
    public bool passedStart;
}

[Serializable]
public sealed class MonoJoeyMoneyDeltaPayload
{
    public string playerId;
    public int delta;
    public int balance;
    public string reason;
    public string counterpartyPlayerId;
    public string tileId;
    public string cardId;
}

[Serializable]
public sealed class MonoJoeyPropertyOwnershipChangePayload
{
    public string tileId;
    public string previousOwnerPlayerId;
    public string newOwnerPlayerId;
    public string reason;
}

[Serializable]
public sealed class MonoJoeyPlayerEliminationPayload
{
    public string playerId;
    public string reason;
    public int money;
    public int paymentDue;
}
