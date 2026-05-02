
# MonoJoey Rules Engine

## Purpose

The rules engine is the authoritative server-side system that validates and resolves every gameplay action.

It must be independent from Unity and testable as plain C#/.NET code.

## Main concepts

### MatchState

Owns current match truth:

- `matchId`
- `status`
- `ruleset`
- `board`
- `players`
- `currentTurnPlayerId`
- `turnNumber`
- `phase`
- `auctionState`
- `deckStates`
- `eventLog`
- `startedAtUtc`
- `endedAtUtc`

### PlayerState

Tracks per-player state:

- `playerId`
- `username`
- `tokenId`
- `colorId`
- `money`
- `currentTileId`
- `ownedPropertyIds`
- `heldCardIds`
- `loanAccount`
- `isBankrupt`
- `placement`
- `turnStats`

### RulesetConfig

Controls game rules:

- Starting money.
- Pass-start reward.
- Auction settings.
- Loan Shark settings.
- Card deck settings.
- Jail/lockup settings.
- Bankruptcy settings.
- Win condition settings.
- Turn timer settings later.

## Turn lifecycle

Default server turn flow:

```text
StartTurn
→ Charge start-of-turn loan interest if Loan Shark mode enabled
→ If player is bankrupt, eliminate and advance turn
→ Wait for RollDiceRequest
→ Server rolls dice
→ Move player
→ Resolve landing tile
→ Resolve any required auction/card/rent/payment
→ EndTurn
→ Advance to next active player
```

## Match phases

Suggested enum:

```text
Lobby
Starting
AwaitingRoll
AnimatingServerEvent
ResolvingTile
AuctionActive
AwaitingPlayerDecision
TurnEnding
Completed
Aborted
```

The exact names may change, but phases must prevent illegal requests.

Example:

- `RollDiceRequest` is valid only when phase is `AwaitingRoll` and sender is current player.
- `PlaceBidRequest` is valid only when phase is `AuctionActive`.

## Mandatory Auction Mode

When `mandatoryAuctionsEnabled = true`, unowned purchasable property spaces always trigger auction. Direct purchase is disabled.

Auction config:

```json
{
  "mandatoryAuctionsEnabled": true,
  "auctionInitialTimerSeconds": 9,
  "auctionBidResetTimerSeconds": 3,
  "auctionMinimumBidIncrement": 10,
  "auctionStartingBid": 10
}
```

Auction state:

```text
NoBidsYet
ActiveBidding
Sold
NoSale
Cancelled
```

Auction rules:

1. Auction starts with no highest bidder.
2. Initial timer uses `auctionInitialTimerSeconds`.
3. If initial timer expires without bids, auction ends `NoSale`.
4. First valid bid sets highest bidder and switches to `ActiveBidding`.
5. Active bidding timer uses `auctionBidResetTimerSeconds`.
6. Every valid bid resets active bidding timer.
7. Expiry during active bidding awards property to highest bidder.
8. Highest bidder pays bank.
9. If winner cannot pay at resolution time, server must either reject impossible bids earlier or handle default according to rules. Prefer rejecting bids above available money unless Loan Shark borrow is explicitly used before bidding.

Bid validation:

- Match phase is `AuctionActive`.
- Property is still unowned.
- Bidder is active/not bankrupt.
- Bid amount is at least starting bid if first bid.
- Bid amount is at least current highest bid + minimum increment if later bid.
- Bidder can afford bid using current cash plus allowed eligible borrowing if rule supports that flow.

## Loan Shark Mode

Loan Shark mode must be configurable and toggleable.

Loan config example:

```json
{
  "loanSharkEnabled": true,
  "baseInterestRate": 0.25,
  "interestRateIncreasePerLoan": 0.10,
  "interestRateIncreasePerDebtTier": 0.05,
  "minimumInterestPayment": 25,
  "canBorrowForLoanPayments": false
}
```

Loan account fields:

- `principalOutstanding`
- `totalBorrowed`
- `totalInterestPaid`
- `loanCount`
- `currentInterestRate`
- `interestDueAtNextTurnStart`

Start-of-turn rule:

```text
If Loan Shark mode enabled and player has interest due:
  charge interest before roll
  if player cannot pay, bankruptcy/elimination is resolved before roll
```

Anti-loop rule:

Players cannot borrow to pay:

- LoanInterest
- LoanPrincipalRepayment
- ExistingLoanDebt

Allowed borrow reasons:

- AuctionBid
- RentPayment
- TaxPayment
- CardPenalty
- Fine

Borrow reason should be explicit in code:

```text
MoneyReason.AuctionBid
MoneyReason.RentPayment
MoneyReason.TaxPayment
MoneyReason.CardPenalty
MoneyReason.Fine
MoneyReason.LoanInterest
MoneyReason.LoanPrincipalRepayment
MoneyReason.ExistingLoanDebt
```

## Card system

Use placeholder decks during core-game phase.

Required action types:

- `MoveToTile`
- `MoveRelative`
- `MoveToNearestType`
- `ReceiveFromBank`
- `PayBank`
- `PayEachOpponent`
- `CollectFromEachOpponent`
- `PayPerUpgrade`
- `HoldableCancelStatus`

Card config fields:

- `cardId`
- `deckId`
- `placeholderName`
- `safeDescription`
- `actionType`
- `amount`
- `target`
- `resolveLandingTile`
- `collectPassStart`
- `holdable`
- `enabled`

Do not write final custom card text yet.

## Placeholder Chance-style deck

- `CHANCE_01` — Move to Start.
- `CHANCE_02` — Move to premium property.
- `CHANCE_03` — Move to selected property A.
- `CHANCE_04` — Move to selected property B.
- `CHANCE_05` — Move to nearest transport.
- `CHANCE_06` — Move to nearest utility.
- `CHANCE_07` — Receive money from bank.
- `CHANCE_08` — Holdable lockup escape.
- `CHANCE_09` — Move back 3 spaces.
- `CHANCE_10` — Go to lockup.
- `CHANCE_11` — Pay per upgrade owned.
- `CHANCE_12` — Pay fixed fine.
- `CHANCE_13` — Move to specific transport.
- `CHANCE_14` — Move to highest-value property.
- `CHANCE_15` — Pay each opponent.
- `CHANCE_16` — Receive money from bank.

## Placeholder Table-style deck

- `TABLE_01` — Move to Start.
- `TABLE_02` — Receive money from bank.
- `TABLE_03` — Pay fixed fee.
- `TABLE_04` — Receive money from bank.
- `TABLE_05` — Holdable lockup escape.
- `TABLE_06` — Go to lockup.
- `TABLE_07` — Collect from each opponent.
- `TABLE_08` — Receive money from bank.
- `TABLE_09` — Receive small refund.
- `TABLE_10` — Collect small amount from each opponent.
- `TABLE_11` — Receive large payout.
- `TABLE_12` — Pay large bill.
- `TABLE_13` — Pay fixed fee.
- `TABLE_14` — Receive small payment.
- `TABLE_15` — Pay per upgrade owned.
- `TABLE_16` — Receive small prize.

## Bankruptcy

Bankruptcy should be server-owned.

Bankruptcy can occur from:

- Rent payment.
- Tax/fine/card payment.
- Auction bid/payment if allowed by rules.
- Loan interest at start of turn.

Loan interest bankruptcy occurs before rolling and eliminates the player before their movement.

## Win condition

The server completes a match deterministically when a persisted terminal action leaves exactly one active
player. Active means the player is not bankrupt and not eliminated.

Rules:

- More than one active player: match remains in progress.
- Exactly one active player: set `GameState.Status = Completed`, `GamePhase.Completed`, `WinnerPlayerId`,
  and `EndedAtUtc`; clear any active auction state.
- Zero active players: match remains unchanged and no winner is declared.
- Already completed states are idempotent and do not emit duplicate completion.

Completion is evaluated after mutation paths that can eliminate players: rent/card tile execution,
auction finalization, and end-turn advancement including start-turn loan interest. A terminal action emits
the normal action event at sequence `N` and `game_completed` at sequence `N + 1`, committed atomically with
the completed `GameState`. Completed matches keep the session in `InGame` status for snapshot/reconnect,
but further gameplay mutations are rejected.

## Stats hooks

Rules engine should emit stat events, not directly own persistence.

Examples:

- `AuctionWon`
- `LoanTaken`
- `LoanInterestPaid`
- `PlayerBankrupted`
- `RentPaid`
- `CardDrawn`
- `DiceRolled`
- `PropertyAcquired`
