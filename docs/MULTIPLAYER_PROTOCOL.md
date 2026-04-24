
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
  "type": "TakeLoanRequest",
  "payload": {
    "matchId": "match_123",
    "amount": 200,
    "reason": "AuctionBid"
  }
}
```

Server validates:

- Loan Shark mode enabled.
- Reason is borrow-eligible.
- Reason is not `LoanInterest`, `LoanPrincipalRepayment`, or `ExistingLoanDebt`.
- Player is active.

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
