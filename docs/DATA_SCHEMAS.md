
# MonoJoey Data Schemas

These are draft schemas for planning. Implementation may use C# records/classes rather than literal JSON.

## RulesetConfig

```json
{
  "rulesetId": "default_v1",
  "displayName": "Default V1",
  "startingMoney": 1500,
  "passStartReward": 200,
  "mandatoryAuctionsEnabled": true,
  "auction": {
    "initialTimerSeconds": 9,
    "bidResetTimerSeconds": 3,
    "minimumBidIncrement": 10,
    "startingBid": 10
  },
  "loanShark": {
    "enabled": true,
    "baseInterestRate": 0.25,
    "interestRateIncreasePerLoan": 0.1,
    "interestRateIncreasePerDebtTier": 0.05,
    "minimumInterestPayment": 25,
    "canBorrowForLoanPayments": false
  },
  "cards": {
    "chanceDeckEnabled": true,
    "tableDeckEnabled": true
  },
  "players": {
    "minPlayers": 2,
    "maxPlayers": 6,
    "uniqueTokensRequired": true,
    "uniqueColorsRequired": true
  }
}
```

## BoardConfig

```json
{
  "boardId": "default_board_v1",
  "version": 1,
  "displayName": "Default Board V1",
  "tileOrder": ["start", "property_01", "chance_01"],
  "tiles": []
}
```

## TileConfig

```json
{
  "tileId": "property_01",
  "index": 1,
  "displayName": "Placeholder Property 01",
  "tileType": "Property",
  "groupId": "group_brown",
  "price": 60,
  "rentTable": [2, 10, 30, 90, 160, 250],
  "upgradeCost": 50,
  "isPurchasable": true,
  "isAuctionable": true
}
```

Tile types:

- `Start`
- `Property`
- `Transport`
- `Utility`
- `ChanceDeck`
- `TableDeck`
- `Tax`
- `Lockup`
- `GoToLockup`
- `FreeSpace`

## CardConfig

```json
{
  "cardId": "CHANCE_01",
  "deckId": "chance",
  "placeholderName": "CHANCE_PLACEHOLDER_01",
  "safeDescription": "Move to Start.",
  "actionType": "MoveToTile",
  "amount": 0,
  "target": "start",
  "resolveLandingTile": false,
  "collectPassStart": true,
  "holdable": false,
  "enabled": true
}
```

Card action types:

- `MoveToTile`
- `MoveRelative`
- `MoveToNearestType`
- `ReceiveFromBank`
- `PayBank`
- `PayEachOpponent`
- `CollectFromEachOpponent`
- `PayPerUpgrade`
- `HoldableCancelStatus`

## TokenConfig

```json
{
  "tokenId": "token_car_placeholder",
  "displayName": "Little Car",
  "modelPrefab": "Token_Car_Placeholder",
  "isPlaceholder": true,
  "enabled": true
}
```

## PlayerProfileSelection

```json
{
  "username": "Josh",
  "tokenId": "token_car_placeholder",
  "colorId": "gold"
}
```

Server must validate uniqueness where required by ruleset.

## MatchState draft

```json
{
  "matchId": "match_123",
  "phase": "AwaitingRoll",
  "rulesetId": "default_v1",
  "boardId": "default_board_v1",
  "currentTurnPlayerId": "player_1",
  "turnNumber": 12,
  "players": [],
  "auctionState": null,
  "startedAtUtc": "2026-04-25T00:00:00Z",
  "endedAtUtc": null
}
```

## PlayerState draft

```json
{
  "playerId": "player_1",
  "username": "Josh",
  "tokenId": "token_car_placeholder",
  "colorId": "gold",
  "money": 1500,
  "currentTileId": "start",
  "ownedPropertyIds": [],
  "heldCardIds": [],
  "loanAccount": {
    "principalOutstanding": 0,
    "loanCount": 0,
    "currentInterestRate": 0,
    "interestDueAtNextTurnStart": 0,
    "totalBorrowed": 0,
    "totalInterestPaid": 0
  },
  "isBankrupt": false
}
```

## AuctionState draft

```json
{
  "auctionId": "auction_123",
  "tileId": "property_01",
  "state": "NoBidsYet",
  "highestBidderPlayerId": null,
  "highestBid": 0,
  "minimumNextBid": 10,
  "timerEndsAtUtc": "2026-04-25T00:00:09Z",
  "initialTimerSeconds": 9,
  "bidResetTimerSeconds": 3
}
```

## MatchResult draft

```json
{
  "matchId": "match_123",
  "boardId": "default_board_v1",
  "rulesetId": "default_v1",
  "startedAtUtc": "2026-04-25T00:00:00Z",
  "endedAtUtc": "2026-04-25T01:12:00Z",
  "durationSeconds": 4320,
  "totalTurns": 80,
  "players": []
}
```

## PlayerLifetimeStats draft

```json
{
  "playerProfileId": "profile_123",
  "gamesPlayed": 0,
  "gamesCompleted": 0,
  "wins": 0,
  "losses": 0,
  "winRate": 0,
  "totalMoneyEarned": 0,
  "totalMoneySpent": 0,
  "totalRentCollected": 0,
  "totalRentPaid": 0,
  "auctionsWon": 0,
  "auctionsLost": 0,
  "biggestAuctionWin": 0,
  "bankruptciesCaused": 0,
  "timesBankrupted": 0,
  "loansTaken": 0,
  "totalLoanDebtBorrowed": 0,
  "totalLoanInterestPaid": 0,
  "loanInterestEliminations": 0,
  "cardsDrawn": 0,
  "doublesRolled": 0,
  "fastestWinSeconds": null,
  "longestGameSeconds": null,
  "comebackWins": 0
}
```
