
# MonoJoey Multiplayer Protocol

## Purpose

Defines the shape of client intentions and server events.

Protocol is WebSocket-based for V1.

## Principle

Client sends requests. Server validates and broadcasts events.

The client must never send final authoritative outcomes.

## Message envelope

Suggested common envelope:

```json
{
  "messageId": "msg_123",
  "type": "PlaceBidRequest",
  "sentAtUtc": "2026-04-25T00:00:00Z",
  "payload": {}
}
```

Server event envelope:

```json
{
  "eventId": "evt_123",
  "sequence": 42,
  "matchId": "match_123",
  "type": "BidAccepted",
  "createdAtUtc": "2026-04-25T00:00:00Z",
  "payload": {}
}
```

Every match event should have a monotonic `sequence` so reconnects can request missing events later.

## Client request types

Lobby:

- `CreateLobbyRequest`
- `JoinLobbyRequest`
- `LeaveLobbyRequest`
- `SetPlayerProfileRequest`
- `SetReadyRequest`
- `StartMatchRequest`

Gameplay:

- `RollDiceRequest`
- `PlaceBidRequest`
- `TakeLoanRequest`
- `UseHeldCardRequest`
- `EndTurnRequest`
- `RequestSnapshot`

Future:

- `TradeOfferRequest`
- `MortgagePropertyRequest`
- `UpgradePropertyRequest`
- `ChatMessageRequest`

## Server event types

Lobby:

- `LobbyCreated`
- `LobbyJoined`
- `LobbyPlayerUpdated`
- `LobbyPlayerReadyChanged`
- `LobbyStartRejected`
- `MatchStarted`

Gameplay:

- `TurnStarted`
- `LoanInterestCharged`
- `PlayerEliminated`
- `DiceRolled`
- `PlayerMoved`
- `TileResolved`
- `AuctionStarted`
- `BidAccepted`
- `BidRejected`
- `AuctionTimerReset`
- `AuctionEndedNoSale`
- `AuctionWon`
- `PropertyTransferred`
- `MoneyChanged`
- `CardDrawn`
- `CardResolved`
- `TurnEnded`
- `MatchCompleted`

State:

- `SnapshotProvided`
- `ErrorEvent`

## Request examples

### Set profile

```json
{
  "type": "SetPlayerProfileRequest",
  "payload": {
    "username": "Josh",
    "tokenId": "token_car_placeholder",
    "colorId": "gold"
  }
}
```

Server validates:

- Username allowed.
- Token available in lobby.
- Color available in lobby.
- Player belongs to lobby.

### Roll dice

```json
{
  "type": "RollDiceRequest",
  "payload": {
    "matchId": "match_123"
  }
}
```

Server validates:

- Sender is current player.
- Phase is `AwaitingRoll`.
- Start-of-turn loan interest has already resolved.

### Place bid

```json
{
  "type": "PlaceBidRequest",
  "payload": {
    "matchId": "match_123",
    "auctionId": "auction_123",
    "amount": 220
  }
}
```

Server validates:

- Auction is active.
- Bidder is active.
- Amount meets minimum increment.
- Bidder can pay or has valid separate borrowing path.

### Take loan

```json
{
  "type": "take_loan",
  "payload": {
    "sessionId": "session_123",
    "playerId": "player_1",
    "amount": 200,
    "reason": "auction_bid"
  }
}
```

Server validates:

- Loan Shark mode enabled.
- Sender connection is bound to the same in-game session/player.
- Player exists in `GameState.Players` and is not eliminated.
- `amount` is a positive integer within safe money/interest bounds.
- `reason` is one of the strict snake_case values: `auction_bid`, `rent_payment`, `tax_payment`, `card_penalty`, `fine`, `loan_interest`, `loan_principal_repayment`, or `existing_loan_debt`.
- `loan_interest`, `loan_principal_repayment`, and `existing_loan_debt` are rejected with `loan_reason_blocked`.
- During an active auction, only `auction_bid` loans are allowed; any bound, non-eliminated game player may take one.
- Outside an active auction, `auction_bid` returns `auction_not_active`; other supported reasons require the current turn player.

Accepted loans return one sender-only `loan_result` and do not place bids automatically:

```json
{
  "type": "loan_result",
  "payload": {
    "playerId": "player_1",
    "amount": 200,
    "reason": "auction_bid",
    "money": 1700,
    "totalBorrowed": 200,
    "currentInterestRatePercent": 20,
    "nextTurnInterestDue": 40,
    "loanTier": 1
  }
}
```

Rejected loans return the standard `error` envelope and do not mutate `GameState`.

### Current `/ws` card tile execution

Card deck tiles reuse the existing server-authoritative `execute_tile` request. Clients do not send deck IDs or card IDs.

```json
{
  "type": "execute_tile",
  "payload": {
    "sessionId": "session_123",
    "playerId": "player_1"
  }
}
```

When the current tile is a chance/table deck placeholder, the server maps the tile type to the fixed deck ID (`chance` or `table`), draws the next persisted card from `GameState.CardDeckStates`, resolves it, executes supported effects atomically, and returns a single sender-only `execute_tile_result`:

```json
{
  "type": "execute_tile_result",
  "payload": {
    "playerId": "player_1",
    "tileId": "chance_01",
    "tileIndex": 2,
    "tileType": "chance_deck",
    "actionKind": "deck_placeholder",
    "executionKind": "card_executed",
    "phase": "awaiting_roll",
    "hasExecutedTileThisTurn": true,
    "auction": null,
    "rent": null,
    "card": {
      "deckId": "chance",
      "cardId": "CHANCE_01_MOVE_TO_START",
      "displayName": "CHANCE_01_MOVE_TO_START",
      "resolutionKind": "move_to_start",
      "executionKind": "card_executed",
      "playerId": "player_1",
      "currentTileId": "start",
      "money": 1500,
      "isEliminated": false,
      "isLockedUp": false,
      "heldCardIds": []
    }
  }
}
```

Card execution kinds are `card_executed`, `card_held`, and `card_payment_eliminated_player`.

Card-specific error codes are `card_deck_not_found`, `card_deck_empty`, `invalid_card`, and `unsupported_card_action`. These errors do not mutate `GameState`, do not draw/discard a card, and do not mark the tile as executed.

## Server event examples

### AuctionStarted

```json
{
  "type": "AuctionStarted",
  "sequence": 104,
  "payload": {
    "auctionId": "auction_123",
    "tileId": "tile_13",
    "propertyName": "Placeholder Property 13",
    "initialTimerSeconds": 9,
    "minimumBidIncrement": 10,
    "startingBid": 10
  }
}
```

### BidAccepted

```json
{
  "type": "BidAccepted",
  "sequence": 105,
  "payload": {
    "auctionId": "auction_123",
    "bidderPlayerId": "player_2",
    "amount": 220,
    "countdownSeconds": 3
  }
}
```

### AuctionWon

```json
{
  "type": "AuctionWon",
  "sequence": 109,
  "payload": {
    "auctionId": "auction_123",
    "winnerPlayerId": "player_2",
    "tileId": "tile_13",
    "amount": 260
  }
}
```

### LoanInterestCharged

```json
{
  "type": "LoanInterestCharged",
  "sequence": 140,
  "payload": {
    "playerId": "player_1",
    "amount": 75,
    "remainingMoney": 120,
    "bankrupted": false
  }
}
```

## Reconnect policy

V1 should support basic reconnect.

Minimum:

- Server keeps match state in memory while match is active.
- Player reconnects with lobby/match identity token.
- Server sends current snapshot.
- Later, use event sequence replay.

## Error policy

Use explicit error codes. Examples:

- `NOT_CURRENT_PLAYER`
- `INVALID_PHASE`
- `AUCTION_NOT_ACTIVE`
- `BID_TOO_LOW`
- `TOKEN_ALREADY_TAKEN`
- `COLOR_ALREADY_TAKEN`
- `LOAN_REASON_BLOCKED`
- `INSUFFICIENT_FUNDS`
- `MATCH_NOT_FOUND`
