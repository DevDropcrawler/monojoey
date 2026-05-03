# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 5
- Chunk: 5.23A Status Effect Foundation
- Completion status: Chunk 5.23A complete; player status effects now have inert server model/storage support and are included in authoritative snapshot/reconnect player payloads. No dice, movement, card, turn, auction, loan, lockup, Slimer, or win-condition behavior was changed.
- Branch: `main` tracking `origin/main`; local has this chunk implemented and validated but not committed.
- Previous commit: `3b4b5ea`
- Last commit before this chunk: `3b4b5ea`
- Last commit after this chunk: not committed yet
- Date/time: 2026-05-03

## Docs Planning Note

- Phase 5.22 planning added `docs/GAME_RULES_SPEC.md` as the canonical customization/control-panel contract for editable rules, presets, custom cards, decks, live-edit safety, future protocol projection, and Slimer/Earthquake extension points. This was docs-only; no backend behavior, Unity UI, voting, card execution, deck editing, Slimer, or Earthquake implementation was added.

## Last Completed Chunk

Phase 5, Chunk 5.23A - Status Effect Foundation.

Completed:

- Added inert status model records under `GameEngine`: `PlayerStatusEffectKind`, `PlayerStatusEffect`, and `PlayerStatusEffectData`.
- Added `Player.StatusEffects` as an empty-by-default read-only list property.
- Added `statusEffects` to `snapshot_result` player payloads and therefore to `reconnect_result.snapshot.players`.
- Preserved empty status collections as `[]`, not `null`.
- Preserved stored status list order during snapshot projection.
- Added snapshot DTOs for status effects using primitive wire fields only: nullable `instanceId`, string `kind`, and data fields `definitionId`, `stackCount`, nullable `remainingTurns`, and nullable `sourceId`.
- Added tests for started-player defaults, empty snapshot arrays, manually stored no-op status projection, reconnect hydration, status JSON round-trip, turn eligibility guard behavior, and completion-manager no-op behavior.
- Verified `dotnet test server-dotnet\MonoJoey.sln -v minimal` passes: 548 passed, 0 failed. Restore emitted NU1900 warnings because vulnerability data could not be fetched from `https://api.nuget.org/v3/index.json`.

Not included by explicit user scope:

- Unity/UI.
- Persistence.
- Stats.
- Unsupported tile effects beyond unowned property auction start, owned property rent, and no-action completion.
- Auction retry handling.
- Scaling.
- Event replay storage and missed-event catch-up.
- Auth, accounts, reconnect tokens, or reconnect secrets.
- Broader lobby broadcasts beyond the `set_profile` profile-sync broadcast.
- Reconnect identity.
- Authentication.
- Unity client.
- Extra turns on doubles.
- Consecutive-doubles lockup behavior.
- Disabled-jail behavior, fine payment, escape logic changes, max-turn aging, or release behavior.
- Slimer behavior, status application, status aging/removal, stacking semantics, status mutation events, Unity client code, property damage, Earthquake, or broad engine refactors.

## Files Changed In This Chunk

- `server-dotnet/MonoJoey.Server/GameEngine/Player.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/PlayerStatusEffect.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/PlayerStatusEffectData.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/PlayerStatusEffectKind.cs`
- `server-dotnet/MonoJoey.Server/Realtime/LobbyMessages.cs`
- `server-dotnet/MonoJoey.Server/Realtime/LobbyMessageHandler.cs`
- `server-dotnet/MonoJoey.Server.Tests/GameEngine/TurnManagerTests.cs`
- `server-dotnet/MonoJoey.Server.Tests/GameEngine/GameCompletionManagerTests.cs`
- `server-dotnet/MonoJoey.Server.Tests/Realtime/LobbyMessageHandlerTests.cs`
- `server-dotnet/MonoJoey.Server.Tests/Sessions/SessionManagerTests.cs`
- `docs/MULTIPLAYER_PROTOCOL.md`
- `docs/SESSION_HANDOVER.md`

## Existing Realtime Files

- `server-dotnet/MonoJoey.Server/Realtime/WebSocketConnection.cs`
- `server-dotnet/MonoJoey.Server/Realtime/IWebSocketConnectionManager.cs`
- `server-dotnet/MonoJoey.Server/Realtime/WebSocketConnectionManager.cs`
- `server-dotnet/MonoJoey.Server/Realtime/WebSocketConnectionHandler.cs`
- `server-dotnet/MonoJoey.Server/Realtime/LobbyConnectionContext.cs`
- `server-dotnet/MonoJoey.Server/Realtime/LobbyMessageHandler.cs`
- `server-dotnet/MonoJoey.Server/Realtime/LobbyMessages.cs`

## Existing Session Files

- `server-dotnet/MonoJoey.Server/Sessions/GameSession.cs`
- `server-dotnet/MonoJoey.Server/Sessions/GameSessionStatus.cs`
- `server-dotnet/MonoJoey.Server/Sessions/GameStateEventPersistenceResult.cs`
- `server-dotnet/MonoJoey.Server/Sessions/PlayerConnection.cs`
- `server-dotnet/MonoJoey.Server/Sessions/SessionManager.cs`

## Existing Engine Files

- `server-dotnet/MonoJoey.Server/GameEngine/AuctionBid.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionBidResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionConfig.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionFinalizationResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionStartResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionStatus.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/BankruptcyManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Board.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/BorrowPurpose.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Card.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/CardActionKind.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/CardActionParameters.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/CardDeck.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/CardDeckIds.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/CardDeckManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/CardDeckState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/CardDrawResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/CardDrawResultKind.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/CardEffectExecutor.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/CardResolutionActionKind.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/CardResolutionResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/CardResolver.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DefaultBoardFactory.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DiceRoll.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DiceService.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/EliminationReason.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/GameCompletionManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/GameState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/IDiceRoller.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/LockupEscapeUseResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/LockupEscapeUseResultKind.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/LockupManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/LoanSharkConfig.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/LoanManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/LoanTakeResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Money.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/MovementManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/MovementResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Player.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/PlayerEliminationResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/PlayerLoanState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/PlaceholderCardDeckFactory.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/PropertyManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/PropertyPurchaseResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/RandomDiceRoller.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/RentPaymentResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Tile.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/TileResolutionActionKind.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/TileResolutionResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/TileResolver.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/TurnManager.cs`

## Validation Commands Run

- `dotnet test server-dotnet\MonoJoey.sln -v minimal`
  - Result: succeeded.
  - Output summary: 548 passed, 0 failed, 0 skipped.
  - Warnings: `NU1900` vulnerability-data lookup could not reach `https://api.nuget.org/v3/index.json`.

## Known Issues

- `NU1900` warnings remain because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json`.
- `AGENTS.md` and `LEAN-CTX.md` were not present at the repo root in this sandbox view, though instructions referenced them.
- `GameRules.Loans` rate-field defaults still do not match current runtime loan manager behavior. This chunk deliberately preserved runtime 20/30/50/+10/100 behavior and left schema/default correction for a separate phase.
- No UI, persistence, stats, event replay, or reconnect catch-up was added.

## Placeholders Introduced Or Preserved

- Session IDs are generated as GUID `N` strings and are not yet public lobby codes or persisted identifiers.
- WebSocket connection IDs are generated as GUID `N` strings and are transport-local only.
- WebSocket connections are stored in memory only; there is no persistence, distributed socket registry, heartbeat, authentication, or reconnect secret.
- `/ws` handles complete text messages as one JSON lobby/gameplay request each and sends one direct response to the sender.
- Successful state-changing gameplay requests then emit one or more best-effort ordered broadcast events to connected in-game players in the same session, including the sender. The direct response is sent first.
- `/ws` rejects binary messages with an `invalid_message` error response.
- Wire message types are server-local snake-case strings for this chunk: `create_lobby`, `join_lobby`, `leave_lobby`, `set_profile`, `set_ready`, `start_game`, `roll_dice`, `resolve_tile`, `execute_tile`, `end_turn`, `place_bid`, `finalize_auction`, `take_loan`, `get_snapshot`, `reconnect_session`, `lobby_state`, `game_started`, `roll_result`, `resolve_tile_result`, `execute_tile_result`, `end_turn_result`, `bid_result`, `auction_result`, `loan_result`, `snapshot_result`, `reconnect_result`, `dice_rolled`, `tile_resolved`, `tile_executed`, `turn_ended`, `bid_accepted`, `auction_finalized`, `loan_taken`, `game_completed`, and `error`.
- `create_lobby` returns an empty lobby state and does not automatically join the creator.
- `join_lobby` binds the WebSocket connection to the joined `playerId`; later attempts by that same socket to use a different `playerId` return `player_switch_rejected`.
- `leave_lobby` requires the WebSocket connection to be bound to the leaving `playerId`.
- `set_profile` requires an already-bound lobby connection and derives `sessionId` and `playerId` only from `LobbyConnectionContext`; payload `sessionId`/`playerId` are not required and are ignored if present.
- `set_profile` requires non-empty string `username`, `tokenId`, and `colorId`; values are trimmed before storage.
- `set_profile` enforces uniqueness within the lobby session, excluding the requesting player: usernames compare case-insensitively, token IDs and color IDs compare ordinally.
- `set_profile` returns direct `lobby_state` and emits a best-effort `lobby_state` broadcast to connected lobby players, including the sender, through the existing WebSocket broadcast path.
- Lobby profile broadcasts use the current `LastEventSequence` value and do not allocate gameplay event sequences.
- Same-player repeated `set_profile` values are idempotent; same-player changes are allowed while the session is still in lobby status.
- Accepted `set_profile` updates are rejected after game start through `invalid_session_status`.
- On WebSocket disconnect, the bound player is removed from the known session only while the session is still in lobby status; after game start, cleanup clears only the socket binding.
- Other lobby messages remain direct request/response only; only `set_profile` currently emits lobby-state broadcasts.
- `/health` returns a minimal plain-text `healthy` response.
- `PlayerConnection.ConnectionId` is populated from the WebSocket transport connection ID for lobby joins and in-game reconnects, but there is still no authenticated identity.
- `PlayerConnection.IsReady`, `Username`, `TokenId`, and `ColorId` are lobby metadata only; `start_game` requires every lobby player to be ready and copies profile metadata into engine players.
- `GameSession.Players` tracks lobby connection metadata and is intentionally separate from `GameState.Players`; joining a session does not create an engine player.
- `start_game` creates engine players from lobby players in current lobby order, with the first lobby player becoming the first current turn player through `TurnManager.StartFirstTurn`.
- Started engine players use selected lobby profiles when present; unset profile fields still fall back to deterministic placeholders: username = `playerId`, token = `token_{playerId}`, color = `color_{playerId}`, starting money = `1500`, and current tile = board start tile.
- `game_started` is a start acknowledgement only; it is not a gameplay snapshot protocol beyond the initial player/turn fields needed for this chunk.
- `roll_dice` rolls dice and moves the current player only.
- `roll_result` contains the rolling `playerId`, two dice values, `total`, `isDouble`, landing tile ID as `newPosition`, `passedStart`, `hasRolledThisTurn`, and optional helper `movement` / `moneyDeltas`.
- `roll_dice` does not resolve landing tiles, execute tile effects, start auctions, draw cards, advance turns, or change `GamePhase`; successful rolls emit `dice_rolled`.
- A second `roll_dice` in the same turn is rejected through `invalid_session_state` while `GameState.HasRolledThisTurn` is true.
- `resolve_tile` passively classifies the current player's current tile only after `HasRolledThisTurn` is true.
- `resolve_tile_result` contains `playerId`, `tileId`, `tileIndex`, string `tileType`, deterministic `requiresAction`, and string `actionKind`.
- `resolve_tile` sets only `GameState.HasResolvedTileThisTurn = true`; it does not execute tile effects, change `GamePhase`, advance turns, or mutate player money, position, ownership, held cards, lockup state, auctions, loans, cards, persistence, or stats. Successful resolves emit `tile_resolved`.
- A second `resolve_tile` in the same turn is rejected through `invalid_session_state` while `GameState.HasResolvedTileThisTurn` is true.
- `execute_tile` requires a successful roll and resolve in the same turn before execution.
- `execute_tile_result` contains `playerId`, `tileId`, `tileIndex`, string `tileType`, string `actionKind`, string `executionKind`, current string `phase`, `hasExecutedTileThisTurn`, nullable `auction`, `rent`, and `card` metadata, plus optional helper `movement`, `moneyDeltas`, `propertyOwnershipChanges`, and `playerEliminations`.
- `execute_tile` starts mandatory auctions for unowned auctionable property placeholders, stores the resulting `AuctionState` in `GameState.ActiveAuctionState`, and schedules the server-owned auction timer from the persisted `TimerEndsAtUtc`; it does not place bids, finalize auctions, transfer auction ownership, or change `GamePhase`. Successful executions emit `tile_executed`.
- `execute_tile` pays base rent for property placeholders owned by another player through `PropertyManager.PayRentForCurrentTile`; insufficient rent uses the existing hard-elimination bankruptcy behavior.
- `execute_tile` reports `rent_not_charged` for self-owned properties and leaves balances unchanged.
- `execute_tile` treats start/free/no-action tiles as no-op execution and only marks `GameState.HasExecutedTileThisTurn = true`.
- `execute_tile` executes chance/table deck placeholders through persisted `GameState.CardDeckStates`; tax placeholders and go-to-lockup placeholders still return `unsupported_tile_effect` without mutating session state.
- Card-tile `execute_tile` maps tile type deterministically to `chance` or `table`, draws the top persisted card, resolves it, executes supported actions, replaces `GameState.CardDeckStates`, marks the tile executed, and returns card metadata in the same `execute_tile_result`.
- Card-tile errors `card_deck_not_found`, `card_deck_empty`, `invalid_card`, and `unsupported_card_action` do not mutate session state and do not mark `GameState.HasExecutedTileThisTurn`.
- A second `execute_tile` in the same turn is rejected through `invalid_session_state` while `GameState.HasExecutedTileThisTurn` is true.
- An already-populated `GameState.ActiveAuctionState` rejects `execute_tile` through `invalid_session_state`.
- `end_turn` requires a successful roll, resolve, execute, no active auction, and a non-eliminated current player before advancing.
- `end_turn_result` contains `previousPlayerId`, nullable `nextPlayerId`, and `turnIndex` from the advanced `GameState.TurnNumber`.
- `end_turn` rejects missing sessions through `invalid_session`, unbound or switched session/player connections through `player_switch_rejected`, lobby/non-game sessions through `invalid_session_state`, missing engine players through `player_not_found`, non-current players through `not_your_turn`, eliminated current players through `player_eliminated`, locked current players before the completed-turn state through `player_locked`, incomplete turn steps through `invalid_session_state`, and active auctions through `invalid_session_state`.
- Locked status is ignored for `end_turn` only in the completed-turn state: `HasRolledThisTurn`, `HasResolvedTileThisTurn`, and `HasExecutedTileThisTurn` are all true and `ActiveAuctionState` is null.
- If `execute_tile` eliminated the current player and the match completed, later gameplay requests return `game_already_completed`; if the match did not complete, `end_turn` still returns `player_eliminated` and does not advance.
- Successful `end_turn` advances only through `TurnManager.AdvanceToNextTurn`, which resets turn flags and `ActiveAuctionState`, and the handler persists that returned state through the same `SessionManager.UpdateGameState` pattern used by `roll_dice`, `resolve_tile`, and `execute_tile`.
- `end_turn` does not emit snapshots, finalize auctions, add persistence, add client behavior, or special-case eliminated players into a forced advance. Successful end-turn actions emit `turn_ended`.
- `place_bid` requires a positive integer `amount`, a bound in-game session/player connection, a non-eliminated engine player, and `ActiveAuctionState.Status` of `AwaitingInitialBid` or `ActiveBidCountdown`.
- `place_bid` allows non-current players to bid, does not require turn ownership, and permits locked non-eliminated players during active auctions.
- Accepted `place_bid` calls update only `GameState.ActiveAuctionState`; no player money, property ownership, turn flags, or phase values are changed.
- `bid_result` is the direct sender response and contains `bidderPlayerId`, `amount`, `currentHighestBid`, `highestBidderId`, `propertyTileId`, `status`, `minimumNextBid`, `bidCount`, `countdownDurationSeconds`, and `timerEndsAtUtc` from the persisted updated active auction state. Accepted bids emit `bid_accepted`.
- Rejected `place_bid` calls return `error` and do not update `GameState`.
- Bid affordability is still not checked in the WebSocket layer; this preserves existing `AuctionManager.PlaceBid` behavior and leaves loan/affordability policy to a later chunk.
- `finalize_auction` requires a bound in-game session/player connection, the current turn player, a non-eliminated caller, and `ActiveAuctionState.Status` of `AwaitingInitialBid` or `ActiveBidCountdown`; locked current players may finalize active auctions.
- Successful `finalize_auction` calls clear `GameState.ActiveAuctionState` and preserve `GamePhase`; turn advancement remains an explicit later `end_turn` request.
- `auction_result` is the direct sender response and contains `resultType` (`won`, `no_sale`, or `failed_payment`), nullable `winnerPlayerId`, `amount`, `tileId`, and optional helper `moneyDeltas`, `propertyOwnershipChanges`, and `playerEliminations`. Successful finalization emits `auction_finalized`, including no-sale outcomes.
- `won` responses reflect the persisted owner after payment and property transfer; `no_sale` responses use `winnerPlayerId = null` and `amount = 0`; `failed_payment` responses identify the failed winner and attempted winning amount while leaving the property unowned.
- Rejected `finalize_auction` calls return `error` and do not update `GameState`.
- `take_loan` requires a positive integer `amount`, a strict snake_case `reason`, a bound in-game session/player connection, a non-eliminated engine player, and enabled `GameState.Rules.Loans.LoanSharkEnabled`.
- `take_loan` accepts only these wire reasons: `auction_bid`, `rent_payment`, `tax_payment`, `card_penalty`, `fine`, `loan_interest`, `loan_principal_repayment`, and `existing_loan_debt`; unknown casing, numeric values, and missing/non-string reasons return `invalid_payload`.
- `take_loan` returns `invalid_loan_amount` for non-positive integer amounts or amounts outside the safe server bound.
- `loan_interest`, `loan_principal_repayment`, and `existing_loan_debt` return `loan_reason_blocked` and do not mutate `GameState`.
- During active auctions, `take_loan` allows any bound, non-eliminated game player to borrow for `auction_bid`, including locked players; it does not require turn ownership and does not place a bid automatically.
- During active auctions, non-auction borrow reasons return `invalid_session_state`.
- Outside active auctions, `auction_bid` returns `auction_not_active`; supported non-auction borrow reasons require the current turn player and reject a locked current player through `player_locked`.
- Accepted `take_loan` calls persist the full `GameState` returned by `LoanManager.TakeLoan`; the handler does not patch money or loan fields.
- `loan_result` is the direct sender response and contains `playerId`, `amount`, strict snake_case `reason`, `money`, `totalBorrowed`, `currentInterestRatePercent`, `nextTurnInterestDue`, and `loanTier` from the persisted player. Accepted loans emit `loan_taken`.
- Rejected `take_loan` calls return `error` and do not update `GameState`.
- `get_snapshot` requires a bound in-game session/player connection and returns `invalid_payload`, `invalid_session`, `player_switch_rejected`, `invalid_session_state`, or `player_not_found` before producing a snapshot.
- `snapshot_result` is sender-only and contains `snapshotVersion = 1`, session/match IDs, `in_game` session status, `gameStatus`, phase, nullable winner, start/end timestamps, turn flags directly from `GameState`, players including inert `statusEffects`, board, nullable active auction, card decks, and loan shark config.
- Completed snapshots have `gameStatus = "completed"`, `phase = "completed"`, `winnerPlayerId`, `endedAtUtc`, and no active auction.
- Snapshot projection is built only from persisted `GameState` while holding the realtime handler `sessionLock`; it does not call mutating managers and does not call `SessionManager.UpdateGameState`.
- Snapshot DTOs fully copy scalar values and arrays; domain records, `GameSession.Players`, WebSocket connection IDs, lobby connection metadata, transport IDs, auth tokens, and reconnect secrets are not exposed.
- Snapshot ordering is deterministic: engine players preserve `GameState.Players` order; owned property IDs and held card IDs sort ascending; card decks sort by deck ID; board tiles sort by index then tile ID; auction bids and deck piles preserve persisted order.
- Snapshot `activeAuction` is `null` when `GameState.ActiveAuctionState` is null.
- `reconnect_session` requires an existing in-memory in-game session, an existing engine player, and existing connection metadata for that player; it returns `invalid_payload`, `invalid_session`, `invalid_session_state`, `player_switch_rejected`, or `player_not_found`.
- `reconnect_result` is sender-only and contains `sessionId`, `playerId`, advisory `lastEventSequence`, and the same authoritative snapshot shape returned by `snapshot_result`.
- Reconnect does not create players, duplicate engine players, restart turns, change phases, mutate `GameState`, replay broadcasts, allocate event sequences, or expose connection IDs in the snapshot.
- Reconnect only works while this server process still has the session in memory. There is no auth, account, token, reconnect secret, persistence, or missed-event replay yet.
- Clients must hydrate from the returned reconnect snapshot rather than applying local assumptions about missed events.
- Match completion reads `GameState.Rules.Win.ConditionType`; only `lastPlayerStanding` currently executes completion behavior.
- For `lastPlayerStanding`, match completion is based only on persisted action state: active player means not bankrupt and not eliminated.
- Unsupported/future win condition values remain schema-only and do not complete a game through `GameCompletionManager`.
- A terminal action emits the normal action event at sequence `N` and `game_completed` at sequence `N + 1`; completed state and `LastEventSequence = N + 1` are committed atomically.
- Completed games keep `GameSessionStatus.InGame`; only `GameState.Status` and `GameState.Phase` move to completed.
- Completed gameplay mutations reject with `game_already_completed` before mutation and without sequence allocation.
- `SessionManager` is an in-memory manager only; it has no persistence, distributed storage, cleanup, scaling, networking, or async behavior.
- Duplicate lobby joins are idempotent by `PlayerId` for membership count; they refresh the stored connection ID while preserving the existing ready state and profile metadata.
- Leaving with a player not present in the session is a safe no-op.
- Leaving after game start can remove lobby connection metadata, but it does not remove any player from `GameState.Players`.
- `AuctionConfig.InitialPreBidSeconds` defaults to `9`; auction start persists `CountdownDurationSeconds = 9` and `TimerEndsAtUtc = now + 9 seconds`.
- `AuctionConfig.BidResetSeconds` defaults to `3`; valid bids copy this value into `AuctionState.CountdownDurationSeconds`, persist `TimerEndsAtUtc = acceptedAtUtc + 3 seconds`, and replace the server-owned timer.
- `AuctionConfig.MinimumBidIncrement` defaults to `1`; bid validation now uses it after the first bid.
- `AuctionConfig.StartingBid` defaults to `0`; first-bid validation now uses it.
- `AuctionStatus.ActiveBidCountdown` is paired with the persisted `TimerEndsAtUtc` deadline and server-owned timer expiry path.
- `AuctionState.CountdownDurationSeconds` stores deterministic countdown duration metadata; `AuctionState.TimerEndsAtUtc` is the persisted deadline authority.
- Placeholder board IDs/display names from Chunk 2.1 are preserved.
- Tile resolution action kinds remain placeholders and do not apply game effects.
- Placeholder card IDs/display names from Chunk 4.1 are functional identifiers only, not final card names or text.
- Placeholder card decks have fixed ordered definitions; `CardDeckState.FromDeck()` preserves that order for deterministic draw behavior.
- Placeholder card action parameters are functional metadata only; tile targets and money amounts are not final card design.
- Empty card draw piles return `CardDrawResultKind.DrawPileEmpty`; discards are not reshuffled yet.
- Card deck state is stored in `GameState.CardDeckStates` using full immutable replacement; deck states are keyed by the `CardDeckIds.Chance` / `CardDeckIds.Table` constants.
- `CardResolutionActionKind.InvalidCard` is a safe resolver output for invalid or incomplete card definitions; WebSocket card execution returns `invalid_card` before persisting any draw, discard, execution flag, or player mutation.
- `CardEffectExecutor` leaves unsupported or out-of-scope resolved card action kinds unchanged, and WebSocket card execution pre-filters those actions as `unsupported_card_action` before calling it.
- Lockup uses the placeholder `lockup_01` tile ID only; there is no advanced jail location selection or custom board lookup beyond requiring that tile to exist.
- `JailRules.FineAmount` and `JailRules.MaxTurns` are serialized configuration only; gameplay does not read them yet.
- Held get-out-of-lockup escapes are stored in `Player.HeldCardIds`; there is no separate inventory, token count, deck discard return, or persistence.
- Using a get-out-of-lockup escape while not locked or without holding that escape returns a typed no-op result and leaves `GameState` unchanged.
- Property rent currently uses base rent only: the first rent table value, or a placeholder `10` for purchasable tiles without a rent table.
- Bankruptcy is hard elimination only; balances are not auto-corrected, no assets are liquidated, and no debt recovery is attempted.
- Loan interest is deducted only at turn start through `LoanManager.StartTurnInterestCheck`; it is not compounded, repaid, or otherwise collected.
- Default loan interest after the third borrow increases by 10 percentage points per loan tier and caps at 100%; these runtime rates live in `LoanSharkConfig` defaults for now, not in `GameRules.Loans` rate fields.
- No protected Monopoly wording, branding, board names, card wording, artwork, or final token assumptions were introduced.

## Important Decisions Preserved

- Server-authoritative rules state.
- Unity remains untouched.
- WebSocket transport lives under `server-dotnet/MonoJoey.Server/Realtime` and is intentionally separate from `Sessions` and `GameEngine`.
- `/ws` is the only WebSocket endpoint.
- WebSocket transport connection IDs are bound to `PlayerConnection.ConnectionId` for active lobby membership created by `join_lobby` and for in-game reconnects through `reconnect_session`.
- One WebSocket connection may bind to only one `playerId`; attempts to switch players are rejected.
- `set_ready` and `start_game` require the WebSocket connection to be bound to the same `sessionId` and `playerId` named in the payload.
- `set_profile` deliberately does not trust payload identity fields; it uses only the already-bound `LobbyConnectionContext` session/player identity.
- Game start is deterministic: engine players are created in lobby order and `TurnManager.StartFirstTurn` selects the first eligible lobby-order player.
- A session that has transitioned to `InGame` rejects further `JoinSession` and `SetReady` calls.
- Disconnect cleanup after game start does not remove engine players and clears only the matching current connection ID, so stale sockets closing after reconnect cannot erase the newer binding.
- Lobby broadcasts exist only for accepted `set_profile` updates; broader join/leave/ready lobby broadcasts are still intentionally deferred.
- Gameplay `roll_dice`, `resolve_tile`, `execute_tile`, `end_turn`, `place_bid`, `finalize_auction`, and `take_loan` now keep direct request/response behavior and emit sequenced broadcasts after successful state changes. `get_snapshot` remains direct-only.
- Roll execution is server-authoritative and reuses `DiceService` and `MovementManager` only.
- Roll execution sets `GameState.HasRolledThisTurn = true` and `GameState.HasResolvedTileThisTurn = false` after movement and does not mutate `GamePhase`.
- Tile resolution is server-authoritative and reuses `TileResolver.ResolveCurrentTile` only.
- Tile resolution sets only `GameState.HasResolvedTileThisTurn = true` after successful passive classification and does not mutate `GamePhase`.
- Tile execution is server-authoritative and re-resolves the current tile through `TileResolver.ResolveCurrentTile` before executing supported effects.
- Tile execution mutates only the narrow supported effect state: rent money/elimination via `PropertyManager`, active auction metadata, and `HasExecutedTileThisTurn`.
- Tile execution does not mutate `GamePhase`; `ActiveAuctionState` is the only Phase 5.7 representation of auction presence.
- End-turn execution is server-authoritative and reuses `TurnManager.AdvanceToNextTurn` only after the current player has rolled, resolved, and executed without an active auction.
- End-turn execution rejects an eliminated current player, including after rent execution eliminates that player.
- End-turn execution uses strict session/player connection binding and does not allow one socket to end another player's turn.
- `HasRolledThisTurn`, `HasResolvedTileThisTurn`, and `HasExecutedTileThisTurn` are reset to false by the existing turn-start boundaries in `TurnManager.StartFirstTurn` and `TurnManager.AdvanceToNextTurn`.
- `ActiveAuctionState` is reset to null by the existing turn-start boundaries in `TurnManager.StartFirstTurn` and `TurnManager.AdvanceToNextTurn`.
- Accepted sockets are registered on connect and removed in a `finally` path after close, WebSocket error, or request cancellation.
- The server host exposes a `Program.BuildApp(args)` seam for host construction without starting `Run()`.
- Session state lives under `server-dotnet/MonoJoey.Server/Sessions` and stays outside `GameEngine`.
- A `GameSession` owns the current rules-engine `GameState` but does not execute turns, resolve messages, or mutate gameplay over a network.
- New sessions start with `GameSessionStatus.Lobby`, `GamePhase.Lobby`, a default board, no connected players, and no engine players.
- Missing sessions are lookup-safe through `GetSession(sessionId) == null`; mutating missing sessions through join/leave is rejected with a clear exception.
- Core game engine code lives under `server-dotnet/MonoJoey.Server/GameEngine`.
- Auctions still produce standalone `AuctionState`; `GameState.ActiveAuctionState` now stores the active mandatory auction started by `execute_tile`, is updated by `place_bid`, and is cleared by successful `finalize_auction`.
- `AuctionManager.PlaceBid` returns a new `AuctionState` inside `AuctionBidResult`; rejected bids return the unchanged auction state.
- `AuctionManager.PlaceBid` requires a caller-supplied `DateTimeOffset` for bid history and does not read wall-clock time.
- Bid validation still does not check bidder cash balance; finalization handles payment failure deterministically.
- `AuctionManager.FinalizeAuction` assumes the auction has already ended; it does not read wall-clock time or own timers. Timer expiry validation lives in `LobbyMessageHandler`.
- Auction finalization selects the highest non-eliminated bidder from bid history.
- Affordable auction winners pay the winning bid and receive ownership through `PropertyManager.AssignOwner`.
- Unaffordable auction winners are eliminated through `BankruptcyManager.EliminateForFailedPayment`; no money is deducted and the property remains unowned.
- `AuctionManager.StartMandatoryAuction` treats `Tile.IsPurchasable && Tile.IsAuctionable` as auction eligibility.
- Disabled mandatory auctions return a typed no-auction result before player/tile lookup.
- Owned properties and non-auctionable tiles return typed no-auction results instead of throwing.
- Unknown triggering players and unknown tiles still throw `InvalidOperationException`, consistent with existing manager validation style.
- Unknown auction bidders return `AuctionBidResultKind.BidderNotInGame` instead of throwing, per Chunk 3.2 rejection scope.
- Ownership continues to live on `Player.OwnedPropertyIds`; no new persistence or aggregate ownership store was added.
- `PropertyManager.AssignOwner` is now used for successful auction ownership transfer.
- Tile resolution remains neutral metadata only and does not mutate gameplay state beyond the WebSocket turn guard flag.
- Dice are server-owned through a service and injectable roller abstraction.
- Movement is deterministic and consumes an already-known step count; it does not roll dice or apply landing effects.
- Loan state lives on `Player.LoanState` and is optional until a player first borrows.
- `LoanManager.TakeLoan` requires an explicit `BorrowPurpose` and `LoanSharkConfig`, and mutates only the borrowing player's money and loan state through a returned `GameState` when the purpose is allowed.
- Loan rejection results return the unchanged `GameState`.
- Borrowing to pay `LoanInterest`, `LoanPrincipalRepayment`, or `ExistingLoanDebt` is rejected through `LoanTakeResultKind.DisallowedBorrowPurpose` unless the runtime config has `CanBorrowForLoanPayments = true`.
- Borrowing remains allowed for `AuctionBid`, `RentPayment`, `TaxPayment`, `CardPenalty`, and `Fine`.
- `NextTurnInterestDue` is calculated from total borrowed and the stored current interest rate using integer money arithmetic.
- Start-of-turn loan interest also uses total borrowed and the stored current interest rate using the same integer money arithmetic.
- `TurnManager.StartFirstTurn` and `TurnManager.AdvanceToNextTurn` derive `LoanSharkConfig` from `GameState.Rules.Loans` and call `LoanManager.StartTurnInterestCheck` before the returned `AwaitingRoll` turn can produce a current player for roll handling.
- `TurnManager.StartFirstTurn` and `TurnManager.AdvanceToNextTurn` skip players whose `IsLockedUp` flag is true.
- `TurnManager.GetCurrentPlayer` rejects a locked current player with `Locked up players cannot take normal turns.`
- Unpaid start-turn interest is a forced deduction; if the resulting balance is negative, existing negative-balance bankruptcy elimination marks the player bankrupt/eliminated.
- Loan enforcement does not interact with auctions, repayment, networking, UI, persistence, or stats.
- Card definitions are passive metadata only: `Card`, `CardDeck`, `CardActionKind`, and `PlaceholderCardDeckFactory` do not mutate `GameState`.
- `CardActionParameters` are passive metadata only and do not execute movement, money changes, lockup changes, or held-card behavior.
- Chance-style and Table-style placeholder decks each contain 16 cards, matching standard property-board-game deck size expectations without copying protected wording.
- `CardActionKind.HoldForLater` maps to `CardResolutionActionKind.GetOutOfLockup`; the card definition/resolver path is still passive, and `CardEffectExecutor` is the boundary that grants the held escape.
- `CardDeckManager.Draw()` and `CardDeckManager.Discard()` return new deck state instances and do not mutate previous deck state.
- Drawing from an empty draw pile returns the unchanged `CardDeckState`; no automatic reshuffle or randomization is implemented.
- Draw and discard logic affects only `CardDeckState`; it does not execute card actions, move players, change money, or alter lockup state.
- WebSocket card tile execution is the integration boundary that composes `CardDeckManager.Draw()`, `CardResolver.ResolveCard()`, `CardEffectExecutor.ExecuteCardEffect()`, and a final immutable `GameState` replacement.
- WebSocket card tile execution first checks `GameState.Rules.Cards.IsDeckEnabled(deckId)` after resolving the deck ID from tile type; disabled deck tiles skip runtime deck-state lookup and return an existing `no_action` execution result while marking the tile executed.
- Card deck gating is per deck only through `CardRules.DecksEnabled`; there is no global cards-enabled flag or derived aggregate helper.
- Supported WebSocket card actions are currently `MoveToStart`, `MoveSteps`, `ReceiveMoney`, `PayMoney`, `GoToLockup`, and `GetOutOfLockup`.
- Out-of-scope resolved actions remain `MoveToTile`, `MoveToNearestTransport`, `MoveToNearestUtility`, `ReceiveMoneyFromEveryPlayer`, `PayMoneyToEveryPlayer`, and `RepairOwnedProperties`.
- Successful non-held cards are appended to that deck's discard pile; successful held escape cards stay only in `Player.HeldCardIds` until a later explicit use path consumes them.
- Missing required parameters for parameterized card actions resolve as `InvalidCard` instead of throwing.
- `CardResolver.ResolveCard(player, card)` maps `CardActionKind` plus card parameters into `CardResolutionResult` only.
- `CardResolver.ResolveCard(player, card)` does not accept `GameState`, does not mutate player state, and does not execute the resolved effect.
- `CardEffectExecutor.ExecuteCardEffect(gameState, cardResolution)` is the first card execution boundary and returns a new `GameState`.
- Card movement execution reuses `MovementManager`; no card-specific position mutation path was added.
- `MovementManager.MovePlayer` now accepts negative steps for relative backward movement and wraps by board length.
- `CardResolutionActionKind.MoveToStart` execution currently moves forward to tile ID `start`, including wrapping when needed, through `MovementManager`.
- `CardResolutionActionKind.MoveSteps` execution can move forward or backward through `MovementManager`.
- `CardResolutionActionKind.ReceiveMoney` and `CardResolutionActionKind.PayMoney` affect only the resolved player.
- `CardResolutionActionKind.PayMoney` deducts first; if the player's balance becomes negative, `BankruptcyManager.EliminateIfBankrupt` marks the player bankrupt/eliminated.
- `CardResolutionActionKind.GoToLockup` directly moves the resolved player to `lockup_01` and marks `IsLockedUp = true`; it does not use `MovementManager` and does not create pass-start money.
- `CardResolutionActionKind.GetOutOfLockup` grants the resolved card ID into `HeldCardIds`; actual escape consumption is performed explicitly through `LockupManager.UseGetOutOfLockupEscape`.
- `CardEffectExecutor` does not draw, discard, reshuffle, advance turns, resolve landing tiles, start auctions, consume lockup escapes, or interact with loans.

## Next Recommended Chunk

Phase 5 follow-up - choose the next narrow networking/session slice only if explicitly requested.

Possible next scopes:

- Resolve the `GameRules.Loans` rate-field/default mismatch in a separate schema-correction phase if that becomes the assigned chunk.
- Bind authenticated/identified connections to `PlayerConnection` only when that chunk is explicitly assigned.
- Add broader lobby broadcasts for join/leave/ready if/when a wider client synchronization chunk is assigned.
- Add auction retry handling, unsupported tile-effect execution, or broader turn-action WebSocket slices only if explicitly assigned.

Recommended validation:

- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`
- `git status --short --branch`

## Do Not Touch Notes

Do not implement before its assigned chunk:

- Client-owned auction timers.
- Persisted runtime timer identifiers or timer versions.
- Event replay storage and reconnect catch-up.
- Lobby broadcasts.
- Non-lobby WebSocket message handling.
- WebSocket authentication or reconnect tokens.
- Broader turn execution over network beyond the explicit roll, resolve, execute, and end-turn slices already implemented.
- Unsupported tile effect execution beyond property auction/rent/no-op and the Phase 5.10 supported card actions.
- UI.
- Persistence.
- Scaling.
- Borrowing to cover auctions beyond the explicit `BorrowPurpose.AuctionBid` context marker.
- Loan repayment.
- Auction retry logic.
- Debt recovery.
- Asset liquidation.
- Automatic card reshuffling.
- Advanced jail/lockup rules beyond the simple status and escape consumption now in place, including disabled-jail behavior, fine payment, escape policy changes, max-turn aging, or release behavior.
- Mortgages.
- Houses/upgrades.
- Trading.
- Taxes/fines money changes.
- Database persistence.
- Stats.
- Unity scenes, prefabs, assets, project settings, metadata, animations, or editor UI.

## Fresh-Session Recommendation

Yes. Chunk 5.22C.7 is complete, and a fresh session should continue from this handover before starting the next assigned Phase 5 chunk.
