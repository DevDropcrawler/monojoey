# MonoJoey Unity Integration Contract

## Purpose And Scope

This document is the canonical backend-to-Unity integration contract for the current MonoJoey backend.
It describes the implemented V1 networking and gameplay boundaries Unity must consume.

This is a documentation-only contract. It does not change backend behavior, protocol shape,
serialization, gameplay rules, Unity project files, or shared protocol placeholders.

The backend is authoritative. Unity is responsible for presentation, local input collection, and sending
player intentions.

Non-goals for this contract:

- No gameplay changes.
- No WebSocket protocol changes.
- No serialization rewrites.
- No Unity project, scene, prefab, asset, or editor changes.
- No promise of future auth, persistence, matchmaking, event replay, custom cards, cosmetics, ranked
  play, moderation, or durable accounts.

## Authority Model

The server owns all match state and gameplay outcomes:

- Turns, turn flags, and current player selection.
- Dice rolls, dice metadata, and movement outcomes.
- Tile classification and supported tile execution.
- Auctions, bids, countdown deadlines, timer expiry, finalization, and ownership transfer.
- Loans, borrow reasons, loan interest, and loan-state projection.
- Lockup state and held escape-card consumption.
- Slimer status effects and their impact on movement.
- Earthquake damage and property repair state.
- Rent, pass-start rewards, money changes, bankruptcy, elimination, completion, and winner selection.
- Stats emission after server-side outcomes.

Unity sends intentions only. It must not send final dice values, final movement paths, final balances,
auction winners, card outcomes, property damage, repair results, bankruptcy decisions, or winner decisions.

Client prediction is visual only. Any anticipation, interpolation, or temporary local state must be
discarded or replaced when an authoritative direct response, broadcast, snapshot, or reconnect snapshot
arrives.

Unity must not maintain hidden gameplay authority. There is no client-owned gameplay state, hidden
balance, hidden position, locally decided outcome, or local source of truth separate from the backend.

## Connection And Sequencing Model

`/ws` is the V1 lobby and gameplay WebSocket endpoint. Each client sends one JSON request per complete
text message. Binary messages are rejected with an `invalid_message` error.

The usual lifecycle is:

1. Connect to `/ws`.
2. Bind the socket through lobby messages such as `join_lobby`, or rebind an in-game player with
   `reconnect_session`.
3. Send one JSON request per text message.
4. Receive one direct response for each request.
5. Receive best-effort broadcasts for relevant lobby or gameplay events.

Disconnect behavior differs by session status. Before game start, disconnect cleanup can remove the
player from lobby membership. After game start, disconnect cleanup does not remove engine players from
`GameState`; it only clears the matching active connection binding.

`reconnect_session` rebinds the current WebSocket connection to an existing in-memory in-game
session/player and returns a direct `reconnect_result` with `lastEventSequence` and an authoritative
snapshot. It does not broadcast, mutate `GameState`, change phase, create players, duplicate engine
players, restart turns, replay events, or allocate a sequence.

`lastEventSequence` is advisory in this version. There is no missed-event replay store. Unity must hydrate
from the returned `snapshot` instead of assuming it can replay missed events.

Gameplay broadcasts have a per-session monotonic `sequence` for successful state-changing gameplay
broadcasts. The first gameplay event sequence in a session is `1`. Rejected requests, malformed payloads,
unsupported actions, `get_snapshot`, `reconnect_session`, lobby actions, and accepted operations that do
not mutate `GameState` do not allocate gameplay sequence numbers.

Unity must process gameplay broadcasts strictly in sequence order for a given session. It must ignore
stale sequence values and must not apply an older or out-of-order broadcast over newer hydrated state.
Snapshot and reconnect hydration supersede buffered event-order assumptions; after hydration, Unity must
treat the snapshot as current truth.

Lobby broadcasts such as `lobby_state` and `rules_updated` reuse the session's current
`lastEventSequence` value and are not gameplay sequence allocations.

## Request And Response Rules

Current client request type strings are snake_case:

- Lobby and setup: `create_lobby`, `join_lobby`, `leave_lobby`, `set_profile`, `set_ready`,
  `set_rules`, `start_game`.
- Gameplay: `roll_dice`, `resolve_tile`, `execute_tile`, `end_turn`, `place_bid`,
  `finalize_auction`, `take_loan`, `mortgage_property`, `unmortgage_property`, `upgrade_property`,
  `use_held_card`, `create_trade_offer`, `accept_trade_offer`, `decline_trade_offer`, `cancel_trade_offer`.
- Recovery: `get_snapshot`, `reconnect_session`.

Current direct response type strings are:

- `lobby_state`, `rules_updated`, `game_started`.
- `roll_result`, `resolve_tile_result`, `execute_tile_result`, `end_turn_result`.
- `bid_result`, `auction_result`, `loan_result`, `mortgage_result`, `unmortgage_result`, `upgrade_result`,
  `use_held_card_result`, `trade_offer_result`, `trade_accept_result`, `trade_decline_result`,
  `trade_cancel_result`.
- `snapshot_result`, `reconnect_result`.
- `error`.

Current broadcast type strings are:

- Lobby/setup: `lobby_state`, `rules_updated`.
- Gameplay: `dice_rolled`, `tile_resolved`, `tile_executed`, `turn_ended`, `bid_accepted`,
  `auction_finalized`, `loan_taken`, `property_mortgaged`, `property_unmortgaged`,
  `property_upgraded`, `held_card_used`, `trade_offer_created`, `trade_offer_accepted`,
  `trade_offer_declined`, `trade_offer_cancelled`, `game_completed`.

One client request produces exactly one direct response to the sender. Successful mutating gameplay
requests then produce separate sequenced broadcasts to connected in-game players, including the sender.
The direct response is sent first.

Rejected requests return a direct `error` response only. They do not mutate `GameState`, do not broadcast,
and do not allocate a gameplay sequence.

Broadcast payloads usually reuse the direct response payload. For example, a successful `roll_dice`
returns direct `roll_result` and then broadcasts `dice_rolled` with the same payload.

Terminal actions can produce two broadcasts in one committed state update: the normal action event at
sequence `N`, followed by `game_completed` at sequence `N + 1`.

The current protocol has no request idempotency key, request correlation ID, or implemented `messageId`.
Unity must not blindly retry state-changing requests after uncertain delivery. If delivery or result state
is uncertain, recover with `get_snapshot` on a bound socket or `reconnect_session` on a new socket.

## Helper Payload Semantics

Helper payload fields are optional convenience data for animation and incremental UI. They are not a
separate source of gameplay authority.

Current helper payloads include:

- `movement`: `playerId`, `fromTileId`, `toTileId`, `pathTileIds`, `stepCount`, `movementKind`,
  `passedStart`.
- `moneyDeltas`: `playerId`, `delta`, `balance`, `reason`, optional `counterpartyPlayerId`, optional
  `tileId`, optional `cardId`.
- `propertyOwnershipChanges`: `tileId`, nullable `previousOwnerPlayerId`, nullable `newOwnerPlayerId`,
  `reason`.
- `playerEliminations`: `playerId`, `reason`, `money`, optional `paymentDue`.
- Auction, rent, and card metadata embedded in action payloads.

Unity must tolerate helper fields being absent, null, or irrelevant for a given event. Optional helper
fields are omitted when irrelevant by the current JSON serializer.

Direct responses, sequenced broadcasts, snapshots, and reconnect snapshots remain authoritative for state.
Snapshots and reconnect are the recovery truth. When helper data and hydrated snapshot data disagree after
reconnect, Unity must use the snapshot.

Omitted helper fields must not cause Unity to preserve stale local data. For example, if a fresh
`execute_tile_result` omits `movement`, Unity should not keep a previous movement helper attached to the
new action.

## Gameplay Flow Contract

`roll_dice`:

- The server validates the bound current player, rolls dice, applies lockup/doubles rules, applies Slimer
  movement rules when movement is allowed, applies pass-start money when relevant, updates turn flags,
  and emits `dice_rolled`.
- `roll_result` includes `playerId`, `dice`, physical two-dice `total`, physical `isDouble`,
  `newPosition`, `passedStart`, `hasRolledThisTurn`, optional `rollKind`, optional
  `jailRollAttemptCount`, and optional helper data.
- For Slimer, movement uses the first die while `dice`, `total`, and `isDouble` remain the physical
  two-dice metadata.
- A failed jail roll does not move the player, leaves them locked, increments jail counters, marks the
  turn complete, and may be ended with `end_turn`.
- A jail doubles release clears lockup, resets jail counters, moves by the physical rolled total, resumes
  the normal resolve/execute flow, and never grants an extra turn from that doubles roll.
- A third consecutive doubles lockup sends the player directly to `lockup_01`, skips movement and landing
  execution, resets the doubles streak, marks the turn complete, and grants no extra turn.

`resolve_tile`:

- The server passively classifies the current player's current tile.
- It sets only the tile-resolved turn flag and emits `tile_resolved`.
- It does not execute tile effects, move money, draw cards, start auctions, or advance turns.

`execute_tile`:

- The server re-resolves and executes the supported current tile effect.
- Supported current effects include no-action tiles, property rent, mandatory auction start for eligible
  unowned property placeholders, and supported chance/table card effects.
- It returns `execute_tile_result` and emits `tile_executed`.
- Payloads include tile/action metadata plus nullable `auction`, `rent`, and `card` metadata and optional
  helpers such as `movement`, `moneyDeltas`, `propertyOwnershipChanges`, and `playerEliminations`.

`end_turn`:

- The server advances only after the current turn is complete with no active auction.
- Doubles grant an extra turn only when `rules.dice.doublesExtraTurnEnabled` is true, the player is not
  locked or eliminated, and the turn was not a jail-release turn. Extra-turn advancement keeps the same
  player and does not re-run start-turn loan interest or automatic property repair.
- Normal next-player advancement applies start-of-turn loan interest and automatic property repair for
  the selected next player.
- `end_turn_result.moneyDeltas` can include `loan_interest` and `property_repair`.

Auctions:

- `execute_tile` starts server-owned mandatory auctions for eligible unowned properties.
- `place_bid` validates and records bids. Accepted bids return `bid_result` and broadcast
  `bid_accepted`.
- `finalize_auction` finalizes active auctions and returns `auction_result`; successful finalization
  emits `auction_finalized`, including no-sale outcomes.
- Countdown durations and `timerEndsAtUtc` are metadata from the server-owned timer/deadline model.

Loans:

- `take_loan` accepts strict snake_case borrow reasons.
- Currently supported borrow reasons are `auction_bid`, `rent_payment`, `tax_payment`, `card_penalty`,
  and `fine`.
- `loan_interest`, `loan_principal_repayment`, and `existing_loan_debt` are blocked by current runtime
  rules and return `loan_reason_blocked`.
- Taking a loan does not automatically place a bid or pay any debt; it only mutates the borrower's money
  and loan state after validation.

Mortgages:

- `mortgage_property` and `unmortgage_property` are server-authoritative direct requests.
- Payload shape is `{ sessionId, playerId, propertyTileId }`.
- The requester must own the property; turn ownership is not required.
- Requests are rejected during active auctions and while the current turn is between tile resolution and tile execution.
- Mortgage value derives from board price using `rules.economy.mortgageValuePercent`.
- Unmortgage cost is mortgage value plus `rules.economy.unmortgageInterestPercent` interest.
- Mortgaged properties remain in `ownedPropertyIds`, set `propertyStates[].data.isMortgaged = true`, and charge no rent.
- Accepted requests return `mortgage_result` or `unmortgage_result`, emit `property_mortgaged` or `property_unmortgaged`, and include `moneyDeltas` with reason `mortgage` or `unmortgage`.

Property upgrades:

- `upgrade_property` is a server-authoritative buy-only request. Sell/downgrade remains deferred.
- Payload shape is `{ sessionId, playerId, propertyTileId }`.
- The requester must be the bound in-game connection, upgrades must be enabled, the game must be in progress, and the player must exist and not be bankrupt or eliminated.
- The requester must own the full buildable group, no group property may be mortgaged, no auction may be active, and the current turn may not be between tile resolution and tile execution.
- Accepted requests return `upgrade_result`, emit `property_upgraded`, deduct `upgradeCost`, persist `propertyStates[].data.upgradeLevel`, and include one `moneyDeltas` entry with reason `property_upgrade`.
- Rejected requests return only `error`; they do not mutate state, broadcast, or allocate a sequence.

Trades:

- `create_trade_offer` stores in-memory pending trade state on the server session and does not mutate `GameState`.
- A player may have one active outgoing offer; a player may receive multiple offers from different proposers.
- `accept_trade_offer` requires the recipient, revalidates current state, then settles through server `TradeManager` primitives.
- `decline_trade_offer` requires the recipient. `cancel_trade_offer` requires the proposer.
- Trade offer order is authoritative by gameplay `sequence` / `createdSequence`; `createdAtUtc` is display/debug metadata only.
- Accepted trade payloads can include `moneyDeltas` and `propertyOwnershipChanges` with reason `trade`.

Lockup and held escape cards:

- Held escape cards live in `heldCardIds`.
- Card execution can grant a held escape card through card metadata and player state when `rules.jail.escapeCardsEnabled` is true.
- The implemented wire request is `use_held_card`.
- The direct response is `use_held_card_result`.
- The broadcast is `held_card_used`.
- Valid use clears lockup state and consumes the held card. Invalid use, including disabled escape cards, returns `error` without mutation.
- Locked players receive turns when `rules.jail.enabled` is true.
- Locked `roll_dice` attempts either release on doubles and move, fail without movement, or at
  `rules.jail.maxTurns` apply the configured failure policy. The currently supported max-turn policy is
  `payFineAndRelease`.
- Max-attempt `payFineAndRelease` deducts `rules.jail.fineAmount`, releases, resets jail counters, moves
  by the failed roll total, and resumes normal resolve/execute flow. If the player cannot pay, no money is
  deducted, the player remains locked, and the completed turn can end deterministically.
- `use_held_card` release suppresses the next doubles extra turn for that same turn.

Slimer:

- Slimer is represented in authoritative player `statusEffects`.
- The current Slimer wire kind and definition ID are `slimer`.
- Slimed players move by the first die on `roll_dice`; physical two-dice `total` and `isDouble` remain
  unchanged in the payload.
- Current server behavior can remove Slimer after a slimed roll whose first die is `6`; Unity must reflect
  the next authoritative player state rather than infer status locally.

Earthquake and repairs:

- Earthquake card execution mutates authoritative `propertyStates` damage for server-selected card
  targets.
- Property damage affects rent on the server.
- Automatic start-turn repairs are server-owned and reflected through `propertyStates` and optional
  `moneyDeltas` with reason `property_repair`.
- There is no client-selected repair request in the current backend.

## Snapshot Contract

`snapshot_result.payload` and `reconnect_result.payload.snapshot` share the same authoritative snapshot
shape.

Current top-level snapshot fields:

- `snapshotVersion`
- `sessionId`
- `status`
- `gameStatus`
- `serverNowUtc`
- `matchId`
- `phase`
- `winnerPlayerId`
- `startedAtUtc`
- `endedAtUtc`
- `turn`
- `players`
- `board`
- `propertyStates`
- `activeAuction`
- `cardDecks`
- `loanShark`
- `rules`
- `pendingTrades`

`turn` fields:

- `currentPlayerId`
- `turnIndex`
- `hasRolledThisTurn`
- `hasResolvedTileThisTurn`
- `hasExecutedTileThisTurn`

Each `players[]` entry includes:

- `playerId`
- `username`
- `tokenId`
- `colorId`
- `money`
- `currentTileId`
- `ownedPropertyIds`
- `heldCardIds`
- `statusEffects`
- `loan`
- `jailTurnCount`
- `jailRollAttemptCount`
- `consecutiveDoublesCount`
- `lastJailReleaseReason`
- `isBankrupt`
- `isEliminated`
- `isLockedUp`

Each `statusEffects[]` entry includes:

- `instanceId`
- `kind`
- `data.definitionId`
- `data.stackCount`
- `data.remainingTurns`
- `data.sourceId`

`statusEffects` is always an array. An empty array means the player has no status effects.

`loan` includes:

- `totalBorrowed`
- `currentInterestRatePercent`
- `nextTurnInterestDue`
- `loanTier`

`board` includes:

- `boardId`
- `version`
- `displayName`
- `tiles`

Each `board.tiles[]` entry includes:

- `tileId`
- `index`
- `displayName`
- `tileType`
- `groupId`
- `price`
- `rentTable`
- `upgradeCost`
- `isPurchasable`
- `isAuctionable`
- `ownerPlayerId`

`propertyStates` is always an array in the current projection. Each entry includes `tileId`,
`data.damagePercent`, and additive `data.isMortgaged` and `data.upgradeLevel`. Current snapshots project
damaged, mortgaged, or upgraded states only; an omitted tile means default undamaged, unmortgaged, and
unupgraded state for that tile. Upgrade projection does not change `snapshotVersion`.

`activeAuction` is null when no auction exists or the match is completed. When present, it includes:

- `propertyTileId`
- `triggeringPlayerId`
- `status`
- `startingBid`
- `minimumBidIncrement`
- `initialPreBidSeconds`
- `bidResetSeconds`
- `highestBid`
- `highestBidderId`
- `countdownDurationSeconds`
- `timerEndsAtUtc`
- `bids`

Each `activeAuction.bids[]` entry includes `bidderPlayerId`, `amount`, and `placedAtUtc`.

Each `cardDecks[]` entry includes `deckId`, `drawPileCardIds`, and `discardPileCardIds`.

`loanShark` currently includes `enabled`.

`rules` is the server's current authoritative rules object for the session. Unity may display it, but must
not use a local copy to override server outcomes.

`pendingTrades` is always an array. Each entry includes `tradeOfferId`, `createdSequence`,
`proposerPlayerId`, `recipientPlayerId`, `offered`, `requested`, and informational `createdAtUtc`.
`offered` and `requested` each include `cash` and `propertyTileIds`. Entries are sorted by
`createdSequence`. Completed snapshots return an empty array.

Optional helper fields are not required for hydration. Snapshot hydration should rebuild Unity's gameplay
presentation from snapshot fields, clearing local state for omitted/default server state.

## Animation Expectations

Unity animates after authoritative direct responses or broadcasts, not before committing permanent state.

Movement animation should use the server `movement` payload when present. `pathTileIds`, `stepCount`,
`movementKind`, and `passedStart` are animation and UI hints tied to the authoritative state change.

Unity interpolation, animation duration, easing, camera timing, anticipation, and presentation-only
prediction are non-authoritative and may differ per client.

If Unity plays temporary anticipation before the server response, it must discard or replace that
presentation with the authoritative payload.

Reconnect and snapshot hydration override all local presentation state, including animations in progress,
cached helper payloads, pending local assumptions, and stale sequence buffers.

## Error Handling

WebSocket errors use this envelope:

```json
{
  "type": "error",
  "payload": {
    "code": "invalid_payload",
    "message": "Human-readable diagnostic."
  }
}
```

Implemented error codes are snake_case:

- `invalid_message`
- `unknown_message_type`
- `invalid_payload`
- `session_not_found`
- `player_switch_rejected`
- `unsupported_message`
- `player_not_in_lobby`
- `invalid_session_status`
- `username_taken`
- `token_taken`
- `color_taken`
- `not_enough_players`
- `players_not_ready`
- `invalid_session`
- `invalid_session_state`
- `not_your_turn`
- `player_not_found`
- `player_eliminated`
- `player_locked`
- `unsupported_tile_effect`
- `auction_not_active`
- `bid_too_low`
- `invalid_loan_amount`
- `invalid_rules`
- `loan_mode_disabled`
- `loan_reason_blocked`
- `mortgage_mode_disabled`
- `upgrade_mode_disabled`
- `property_not_owned`
- `property_already_mortgaged`
- `property_not_mortgaged`
- `insufficient_cash`
- `card_deck_not_found`
- `card_deck_empty`
- `invalid_card`
- `unsupported_card_action`
- `game_already_completed`
- `held_card_not_held`
- `held_cards_disabled`
- `trade_offer_active`
- `trade_offer_not_found`
- `trade_offer_not_for_player`

Rejected gameplay requests do not mutate `GameState`, do not broadcast, and do not allocate a gameplay
sequence.

After transport uncertainty, Unity should recover with `get_snapshot` or `reconnect_session` rather than
replaying assumptions or retrying state-changing requests blindly.

## Stats And Leaderboard APIs

The server exposes read-only HTTP stats APIs:

- `GET /stats/leaderboards/{category}?limit=n`
- `GET /stats/players/{playerId}`

These APIs are non-gameplay and non-authoritative for match state. They must not drive Unity gameplay
decisions.

Current leaderboard categories:

- `wins`
- `rent_paid`
- `rent_collected`
- `auctions_won`
- `loans_taken`
- `cards_triggered`

Current player stats expose:

- `playerId`
- `gamesWon`
- `rentPaid`
- `rentCollected`
- `auctionsWon`
- `loansTaken`
- `cardsTriggered`
- `slimerApplied`
- `earthquakePropertiesDamaged`
- `propertyRepairSpend`

The current stats repository is in-memory. It is presentation data for the running server process, not a
durable account or match-state authority.

## Deferred And Future Systems

The following remain future systems and are not part of the current Unity contract:

- Durable account identity.
- Authentication, authorization, reconnect secrets, or reconnect tokens.
- Cross-process persistence.
- Event replay or missed-event catch-up.
- Matchmaking.
- Upgrades, asset liquidation, loan repayment, or debt recovery.
- Client-selected repairs.
- Custom card editing or user-defined runtime cards.
- Cosmetics, ranked play, moderation, chat, or durable social features.
- Unity-side gameplay authority.

Unity must not build assumptions that these systems exist in the current backend.

## Non-Negotiable Rules

- No gameplay authority in Unity.
- No hidden client state.
- Backend direct responses and broadcasts are action truth.
- Backend snapshots and reconnect snapshots are recovery truth.
- Helper payloads are optional animation/UI hints.
- Stats are read-only presentation data, never gameplay authority.
