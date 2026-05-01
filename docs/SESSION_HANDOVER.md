# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 5
- Chunk: 5.9B Auction finalization over WebSocket
- Completion status: Chunk 5.9B complete; `/ws` now accepts sender-only `finalize_auction` from the current turn player during an active `GameState.ActiveAuctionState`, calls `AuctionManager.FinalizeAuction`, persists the manager-returned `GameState` with `ActiveAuctionState = null`, and returns one direct `auction_result` or `error` response.
- Branch: `main` tracking `origin/main`; local has this chunk implemented and validated but not committed.
- Previous commit: `phase-5-6: add resolve tile websocket action`
- Last commit before this chunk: `phase-5-6: add resolve tile websocket action`
- Last commit after this chunk: `phase-5-7: add execute tile websocket action`
- Date/time: 2026-05-02 00:00 +12:00

## Last Completed Chunk

Phase 5, Chunk 5.9B - WebSocket auction finalization.

Completed:

- Added server-local `finalize_auction` WebSocket message handling and direct `auction_result` responses.
- Added `AuctionResultPayload` with `resultType`, nullable `winnerPlayerId`, `amount`, and `tileId`.
- Validated finalization requests for payload fields, session existence, strict bound connection/session/player, in-game status, player presence, current turn ownership, non-eliminated caller, active auction presence, and active auction status.
- Allowed finalization only for `AwaitingInitialBid` and `ActiveBidCountdown` auctions; missing or unknown-status auctions return `auction_not_active`.
- Explicitly mapped `AuctionManager.FinalizeAuction` results: `FinalizedWithWinner` to `won`, `FinalizedNoWinner` to `no_sale`, `WinnerFailedToPay` to `failed_payment`, and `InvalidAuctionState` to `invalid_session_state`.
- Persisted successful finalization using the full `GameState` returned by `AuctionManager.FinalizeAuction`, then clearing only `ActiveAuctionState` in that same update.
- Built successful responses from the persisted `GameState`, capturing the auction tile from the finalization result before clearing active auction state.
- Preserved sender-only behavior; no broadcasts, timers, automatic expiry, snapshots, reconnect behavior, client work, or persistence were added.
- Added deterministic handler and WebSocket transport tests for won auctions, no-sale auctions, failed payment, eliminated bidders, invalid active auction state, wrong bound connection, non-current player rejection, lobby-session rejection, unsupported client-sent `auction_result`, one response per `finalize_auction`, and `end_turn` succeeding after finalization clears `ActiveAuctionState`.

Not included by explicit user scope:

- Unity/UI.
- Persistence.
- Stats.
- Unsupported tile effects beyond unowned property auction start, owned property rent, and no-action completion.
- Automatic auction expiry or retry handling.
- Scaling.
- Broadcasts.
- Protocol DTO changes.
- Reconnect identity.
- Authentication.
- Unity client.

## Files Changed In This Chunk

- `server-dotnet/MonoJoey.Server/Realtime/LobbyMessageHandler.cs`
- `server-dotnet/MonoJoey.Server/Realtime/LobbyMessages.cs`
- `server-dotnet/MonoJoey.Server.Tests/Realtime/LobbyMessageHandlerTests.cs`
- `server-dotnet/MonoJoey.Server.Tests/Realtime/WebSocketConnectionHandlerTests.cs`
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
- `server-dotnet/MonoJoey.Server/GameEngine/GameState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/IDiceRoller.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/LockupEscapeUseResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/LockupEscapeUseResultKind.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/LockupManager.cs`
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

- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
  - Result: succeeded.
  - Output summary: build succeeded, 2 warnings, 0 errors.
  - Warnings: `NU1900` vulnerability-data lookup could not reach `https://api.nuget.org/v3/index.json`.
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`
  - Result: succeeded.
  - Output summary: 294 tests passed.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `git diff --check`
  - Result: succeeded.
  - Output summary: no whitespace errors; Git reported expected LF-to-CRLF working-copy warnings.
- `git status --short --branch`
  - Result after validation: `main...origin/main` with modified `docs/SESSION_HANDOVER.md`, realtime source files, and focused realtime tests.

## Known Issues

- Plain `dotnet build .\server-dotnet\MonoJoey.sln` and plain `dotnet test .\server-dotnet\MonoJoey.sln` can fail in this Windows shell with no MSBuild errors once the server project participates in the solution graph.
- Serialized validation with `-m:1` succeeds and should be used unless the build harness is revisited.
- `NU1900` warnings remain because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json`.
- `AGENTS.md` and `LEAN-CTX.md` were not present at the repo root in this sandbox view, though instructions referenced them.
- No endpoint integration test was added because the existing test project does not include ASP.NET Core test host packages and this chunk avoided adding NuGet dependencies.

## Placeholders Introduced Or Preserved

- Session IDs are generated as GUID `N` strings and are not yet public lobby codes or persisted identifiers.
- WebSocket connection IDs are generated as GUID `N` strings and are transport-local only.
- WebSocket connections are stored in memory only; there is no persistence, distributed socket registry, heartbeat, authentication, or reconnect binding.
- `/ws` handles complete text messages as one JSON lobby/gameplay request each and sends one direct response to the sender.
- `/ws` rejects binary messages with an `invalid_message` error response.
- Wire message types are server-local snake-case strings for this chunk: `create_lobby`, `join_lobby`, `leave_lobby`, `set_ready`, `start_game`, `roll_dice`, `resolve_tile`, `execute_tile`, `end_turn`, `place_bid`, `finalize_auction`, `lobby_state`, `game_started`, `roll_result`, `resolve_tile_result`, `execute_tile_result`, `end_turn_result`, `bid_result`, `auction_result`, and `error`.
- `create_lobby` returns an empty lobby state and does not automatically join the creator.
- `join_lobby` binds the WebSocket connection to the joined `playerId`; later attempts by that same socket to use a different `playerId` return `player_switch_rejected`.
- `leave_lobby` requires the WebSocket connection to be bound to the leaving `playerId`.
- On WebSocket disconnect, the bound player is removed from the known session only while the session is still in lobby status; after game start, cleanup clears only the socket binding.
- Lobby responses are sender-only; no other connected clients receive state updates in this chunk.
- `/health` returns a minimal plain-text `healthy` response.
- `PlayerConnection.ConnectionId` is now populated from the WebSocket transport connection ID for lobby joins, but there is still no reconnect protocol or authenticated identity.
- `PlayerConnection.IsReady` is lobby metadata only; `start_game` requires every lobby player to be ready.
- `GameSession.Players` tracks lobby connection metadata and is intentionally separate from `GameState.Players`; joining a session does not create an engine player.
- `start_game` creates engine players from lobby players in current lobby order, with the first lobby player becoming the first current turn player through `TurnManager.StartFirstTurn`.
- Started engine players use temporary deterministic placeholders: username = `playerId`, token = `token_{playerId}`, color = `color_{playerId}`, starting money = `1500`, and current tile = board start tile.
- `game_started` is a start acknowledgement only; it is not a gameplay snapshot protocol beyond the initial player/turn fields needed for this chunk.
- `roll_dice` rolls dice and moves the current player only.
- `roll_result` contains the rolling `playerId`, two dice values, landing tile ID as `newPosition`, `passedStart`, and `hasRolledThisTurn`.
- `roll_dice` does not resolve landing tiles, execute tile effects, start auctions, draw cards, advance turns, broadcast state, or change `GamePhase`.
- A second `roll_dice` in the same turn is rejected through `invalid_session_state` while `GameState.HasRolledThisTurn` is true.
- `resolve_tile` passively classifies the current player's current tile only after `HasRolledThisTurn` is true.
- `resolve_tile_result` contains `playerId`, `tileId`, `tileIndex`, string `tileType`, deterministic `requiresAction`, and string `actionKind`.
- `resolve_tile` sets only `GameState.HasResolvedTileThisTurn = true`; it does not execute tile effects, change `GamePhase`, advance turns, broadcast, or mutate player money, position, ownership, held cards, lockup state, auctions, loans, cards, persistence, or stats.
- A second `resolve_tile` in the same turn is rejected through `invalid_session_state` while `GameState.HasResolvedTileThisTurn` is true.
- `execute_tile` requires a successful roll and resolve in the same turn before execution.
- `execute_tile_result` contains `playerId`, `tileId`, `tileIndex`, string `tileType`, string `actionKind`, string `executionKind`, current string `phase`, `hasExecutedTileThisTurn`, and nullable `auction` / `rent` metadata.
- `execute_tile` starts mandatory auctions for unowned auctionable property placeholders and stores the resulting `AuctionState` in `GameState.ActiveAuctionState`; it does not place bids, finalize auctions, transfer auction ownership, start timers, broadcast, or change `GamePhase`.
- `execute_tile` pays base rent for property placeholders owned by another player through `PropertyManager.PayRentForCurrentTile`; insufficient rent uses the existing hard-elimination bankruptcy behavior.
- `execute_tile` reports `rent_not_charged` for self-owned properties and leaves balances unchanged.
- `execute_tile` treats start/free/no-action tiles as no-op execution and only marks `GameState.HasExecutedTileThisTurn = true`.
- `execute_tile` returns `unsupported_tile_effect` without mutating session state for chance/table deck placeholders, tax placeholders, and go-to-lockup placeholders.
- A second `execute_tile` in the same turn is rejected through `invalid_session_state` while `GameState.HasExecutedTileThisTurn` is true.
- An already-populated `GameState.ActiveAuctionState` rejects `execute_tile` through `invalid_session_state`.
- `end_turn` requires a successful roll, resolve, and execute in the same turn before advancing.
- `end_turn_result` contains `previousPlayerId`, nullable `nextPlayerId`, and `turnIndex` from the advanced `GameState.TurnNumber`.
- `end_turn` rejects missing sessions through `invalid_session`, unbound or switched session/player connections through `player_switch_rejected`, lobby/non-game sessions through `invalid_session_state`, missing engine players through `player_not_found`, non-current players through `not_your_turn`, eliminated current players through `player_eliminated`, incomplete turn steps through `invalid_session_state`, and active auctions through `invalid_session_state`.
- If `execute_tile` eliminated the current player, `end_turn` returns `player_eliminated` and does not advance.
- Successful `end_turn` advances only through `TurnManager.AdvanceToNextTurn`, which resets turn flags and `ActiveAuctionState`, and the handler persists that returned state through the same `SessionManager.UpdateGameState` pattern used by `roll_dice`, `resolve_tile`, and `execute_tile`.
- `end_turn` does not broadcast, emit snapshots, finalize auctions, add reconnect behavior, add persistence, add client behavior, or special-case eliminated players into a forced advance.
- `place_bid` requires a positive integer `amount`, a bound in-game session/player connection, a non-eliminated engine player, and `ActiveAuctionState.Status` of `AwaitingInitialBid` or `ActiveBidCountdown`.
- `place_bid` allows non-current players to bid and does not require turn ownership.
- Accepted `place_bid` calls update only `GameState.ActiveAuctionState`; no player money, property ownership, turn flags, or phase values are changed.
- `bid_result` is sender-only and contains `bidderPlayerId`, `amount`, `currentHighestBid`, and `highestBidderId` from the persisted updated active auction state.
- Rejected `place_bid` calls return `error` and do not update `GameState`.
- Bid affordability is still not checked in the WebSocket layer; this preserves existing `AuctionManager.PlaceBid` behavior and leaves loan/affordability policy to a later chunk.
- `finalize_auction` requires a bound in-game session/player connection, the current turn player, a non-eliminated caller, and `ActiveAuctionState.Status` of `AwaitingInitialBid` or `ActiveBidCountdown`.
- Successful `finalize_auction` calls clear `GameState.ActiveAuctionState` and preserve `GamePhase`; turn advancement remains an explicit later `end_turn` request.
- `auction_result` is sender-only and contains `resultType` (`won`, `no_sale`, or `failed_payment`), nullable `winnerPlayerId`, `amount`, and `tileId`.
- `won` responses reflect the persisted owner after payment and property transfer; `no_sale` responses use `winnerPlayerId = null` and `amount = 0`; `failed_payment` responses identify the failed winner and attempted winning amount while leaving the property unowned.
- Rejected `finalize_auction` calls return `error` and do not update `GameState`.
- `SessionManager` is an in-memory manager only; it has no persistence, distributed storage, cleanup, scaling, networking, or async behavior.
- Duplicate joins are idempotent by `PlayerId`; they do not replace the existing connection ID or ready state yet.
- Leaving with a player not present in the session is a safe no-op.
- Leaving after game start can remove lobby connection metadata, but it does not remove any player from `GameState.Players`.
- `AuctionConfig.InitialPreBidSeconds` defaults to `9`; still no real countdown loop exists.
- `AuctionConfig.BidResetSeconds` defaults to `3`; valid bids now copy this value into `AuctionState.CountdownDurationSeconds`, but no actual time passes.
- `AuctionConfig.MinimumBidIncrement` defaults to `1`; bid validation now uses it after the first bid.
- `AuctionConfig.StartingBid` defaults to `0`; first-bid validation now uses it.
- `AuctionStatus.ActiveBidCountdown` is metadata only; it does not trigger asynchronous behavior.
- `AuctionState.CountdownDurationSeconds` stores deterministic countdown duration metadata only.
- Placeholder board IDs/display names from Chunk 2.1 are preserved.
- Tile resolution action kinds remain placeholders and do not apply game effects.
- Placeholder card IDs/display names from Chunk 4.1 are functional identifiers only, not final card names or text.
- Placeholder card decks have fixed ordered definitions; `CardDeckState.FromDeck()` preserves that order for deterministic draw behavior.
- Placeholder card action parameters are functional metadata only; tile targets and money amounts are not final card design.
- Empty card draw piles return `CardDrawResultKind.DrawPileEmpty`; discards are not reshuffled yet.
- Card deck state is standalone and is not stored in `GameState` yet.
- `CardResolutionActionKind.InvalidCard` is a safe resolver output for invalid or incomplete card definitions; it does not mutate state or discard cards.
- `CardEffectExecutor` leaves unsupported or out-of-scope resolved card action kinds unchanged in this chunk.
- Lockup uses the placeholder `lockup_01` tile ID only; there is no advanced jail location selection or custom board lookup beyond requiring that tile to exist.
- Held get-out-of-lockup escapes are stored in `Player.HeldCardIds`; there is no separate inventory, token count, deck discard return, or persistence.
- Using a get-out-of-lockup escape while not locked or without holding that escape returns a typed no-op result and leaves `GameState` unchanged.
- Property rent currently uses base rent only: the first rent table value, or a placeholder `10` for purchasable tiles without a rent table.
- Bankruptcy is hard elimination only; balances are not auto-corrected, no assets are liquidated, and no debt recovery is attempted.
- Loan interest is deducted only at turn start through `LoanManager.StartTurnInterestCheck`; it is not compounded, repaid, or otherwise collected.
- Loan interest after the third borrow increases by 10 percentage points per loan tier and caps at 100%.
- No protected Monopoly wording, branding, board names, card wording, artwork, or final token assumptions were introduced.

## Important Decisions Preserved

- Server-authoritative rules state.
- Unity remains untouched.
- WebSocket transport lives under `server-dotnet/MonoJoey.Server/Realtime` and is intentionally separate from `Sessions` and `GameEngine`.
- `/ws` is the only WebSocket endpoint.
- WebSocket transport connection IDs are bound to `PlayerConnection.ConnectionId` only for the active lobby membership created by `join_lobby`.
- One WebSocket connection may bind to only one `playerId`; attempts to switch players are rejected.
- `set_ready` and `start_game` require the WebSocket connection to be bound to the same `sessionId` and `playerId` named in the payload.
- Game start is deterministic: engine players are created in lobby order and `TurnManager.StartFirstTurn` selects the first eligible lobby-order player.
- A session that has transitioned to `InGame` rejects further `JoinSession` and `SetReady` calls.
- Disconnect cleanup after game start does not mutate match membership or remove engine players.
- Lobby messages are direct request/response only; broadcasts are intentionally deferred.
- Gameplay `roll_dice`, `resolve_tile`, `execute_tile`, and `end_turn` messages are also direct request/response only; broadcasts remain intentionally deferred.
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
- `AuctionManager.FinalizeAuction` assumes the auction has already ended; it does not read wall-clock time or advance timers.
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
- `LoanManager.TakeLoan` requires an explicit `BorrowPurpose` and mutates only the borrowing player's money and loan state through a returned `GameState` when the purpose is allowed.
- Loan rejection results return the unchanged `GameState`.
- Borrowing to pay `LoanInterest`, `LoanPrincipalRepayment`, or `ExistingLoanDebt` is rejected through `LoanTakeResultKind.DisallowedBorrowPurpose`.
- Borrowing remains allowed for `AuctionBid`, `RentPayment`, `TaxPayment`, `CardPenalty`, and `Fine`.
- `NextTurnInterestDue` is calculated from total borrowed and the stored current interest rate using integer money arithmetic.
- Start-of-turn loan interest also uses total borrowed and the stored current interest rate using the same integer money arithmetic.
- `TurnManager.StartFirstTurn` and `TurnManager.AdvanceToNextTurn` call `LoanManager.StartTurnInterestCheck` before the returned `AwaitingRoll` turn can produce a current player for roll handling.
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

- Bind authenticated/identified connections to `PlayerConnection` only when that chunk is explicitly assigned.
- Add lobby broadcasts if/when a broader client synchronization chunk is assigned.
- Add automatic auction timers/expiry, gameplay snapshots, unsupported tile-effect execution, or broader turn-action WebSocket slices only if explicitly assigned.

Recommended validation:

- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`
- `git status --short --branch`

## Do Not Touch Notes

Do not implement before its assigned chunk:

- Real wall-clock timers.
- Async countdown loop.
- Broadcasts.
- Non-lobby WebSocket message handling.
- WebSocket authentication or reconnect tokens.
- Broader turn execution over network beyond the explicit roll, resolve, execute, and end-turn slices already implemented.
- Unsupported tile effect execution beyond Phase 5.7's property auction/rent/no-op execution.
- UI.
- Persistence.
- Scaling.
- Borrowing to cover auctions beyond the explicit `BorrowPurpose.AuctionBid` context marker.
- Loan repayment.
- Auction retry logic.
- Debt recovery.
- Asset liquidation.
- Automatic card reshuffling.
- Advanced jail/lockup rules beyond the simple status and escape consumption now in place.
- Mortgages.
- Houses/upgrades.
- Trading.
- Taxes/fines money changes.
- Database persistence.
- Stats.
- Unity scenes, prefabs, assets, project settings, metadata, animations, or editor UI.

## Fresh-Session Recommendation

Yes. Chunk 5.8 is complete, and a fresh session should continue from this handover before starting the next assigned Phase 5 chunk.
