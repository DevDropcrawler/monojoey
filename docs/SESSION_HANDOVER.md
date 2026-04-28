# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 5
- Chunk: 5.5 Turn actions over WebSocket - roll dice only
- Completion status: Chunk 5.5 complete; `/ws` now accepts sender-only `roll_dice`, validates the bound in-game current player, executes server-owned dice plus movement only, persists `HasRolledThisTurn`, and returns a direct `roll_result` or `error` response.
- Branch: `main` tracking `origin/main`; local has this chunk implemented and validated for commit.
- Previous commit: `phase-5-2: add websocket server foundation`
- Last commit before this chunk: `phase-5-3: add lobby websocket messages`
- Last commit after this chunk: `phase-5-5: add roll dice websocket action`
- Date/time: 2026-04-28 17:20 +12:00

## Last Completed Chunk

Phase 5, Chunk 5.5 - WebSocket roll dice action only.

Completed:

- Added server-local `roll_dice` WebSocket message handling.
- Added direct `roll_result` response payloads containing `playerId`, dice values, `newPosition`, `passedStart`, and `hasRolledThisTurn`.
- Added `GameState.HasRolledThisTurn` and reset it when `TurnManager.StartFirstTurn` or `TurnManager.AdvanceToNextTurn` starts an awaiting-roll turn.
- Added `SessionManager.UpdateGameState` as the narrow persistence seam for game-state updates after movement.
- Registered `IDiceRoller`, `RandomDiceRoller`, and `DiceService` in the server host.
- Reused existing `DiceService` and `MovementManager`; no duplicate dice or movement logic was added.
- Validated roll requests for session existence, in-game status, bound connection/session/player, current turn player, eliminated player, locked player, and duplicate same-turn roll.
- Preserved the current `GamePhase`; rolling does not transition to resolving, auction, card, or end-turn phases.
- Preserved sender-only behavior; no broadcasts were added.
- Added deterministic handler and transport tests for successful rolls, movement, pass-start reporting, duplicate roll rejection, player/session guards, and one response per roll request.

Not included by explicit user scope:

- Unity/UI.
- Persistence.
- Stats.
- Tile resolution.
- Tile effects.
- Turn advancement or end-turn handling.
- Scaling.
- Broadcasts.
- Protocol DTO changes.
- Reconnect identity.
- Authentication.
- Unity client.

## Files Changed In This Chunk

- `server-dotnet/MonoJoey.Server/Realtime/LobbyMessageHandler.cs`
- `server-dotnet/MonoJoey.Server/Realtime/LobbyMessages.cs`
- `server-dotnet/MonoJoey.Server/Program.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/GameState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/TurnManager.cs`
- `server-dotnet/MonoJoey.Server/Sessions/SessionManager.cs`
- `server-dotnet/MonoJoey.Server.Tests/Realtime/LobbyMessageHandlerTests.cs`
- `server-dotnet/MonoJoey.Server.Tests/Realtime/WebSocketConnectionHandlerTests.cs`
- `server-dotnet/MonoJoey.Server.Tests/GameEngine/TurnManagerTests.cs`
- `server-dotnet/MonoJoey.Server.Tests/Sessions/SessionManagerTests.cs`
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
  - Output summary: 215 tests passed.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `git diff --check`
  - Result: succeeded.
  - Output summary: no whitespace errors; Git reported expected LF-to-CRLF working-copy warnings.
- `git status --short --branch`
  - Result after validation: modified realtime/session source files, focused tests, and handover file pending commit.

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
- Wire message types are server-local snake-case strings for this chunk: `create_lobby`, `join_lobby`, `leave_lobby`, `set_ready`, `start_game`, `roll_dice`, `lobby_state`, `game_started`, `roll_result`, and `error`.
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
- `roll_dice` is the only gameplay action handled over WebSocket; it rolls dice and moves the current player only.
- `roll_result` contains the rolling `playerId`, two dice values, landing tile ID as `newPosition`, `passedStart`, and `hasRolledThisTurn`.
- `roll_dice` does not resolve landing tiles, execute tile effects, start auctions, draw cards, advance turns, broadcast state, or change `GamePhase`.
- A second `roll_dice` in the same turn is rejected through `invalid_session_state` while `GameState.HasRolledThisTurn` is true.
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
- Gameplay `roll_dice` messages are also direct request/response only; broadcasts remain intentionally deferred.
- Roll execution is server-authoritative and reuses `DiceService` and `MovementManager` only.
- Roll execution sets `GameState.HasRolledThisTurn = true` after movement and does not mutate `GamePhase`.
- `HasRolledThisTurn` is reset to false by the existing turn-start boundaries in `TurnManager.StartFirstTurn` and `TurnManager.AdvanceToNextTurn`.
- Accepted sockets are registered on connect and removed in a `finally` path after close, WebSocket error, or request cancellation.
- The server host exposes a `Program.BuildApp(args)` seam for host construction without starting `Run()`.
- Session state lives under `server-dotnet/MonoJoey.Server/Sessions` and stays outside `GameEngine`.
- A `GameSession` owns the current rules-engine `GameState` but does not execute turns, resolve messages, or mutate gameplay over a network.
- New sessions start with `GameSessionStatus.Lobby`, `GamePhase.Lobby`, a default board, no connected players, and no engine players.
- Missing sessions are lookup-safe through `GetSession(sessionId) == null`; mutating missing sessions through join/leave is rejected with a clear exception.
- Core game engine code lives under `server-dotnet/MonoJoey.Server/GameEngine`.
- Auctions still produce standalone `AuctionState`; `GameState` is not mutated and does not yet store active auction state.
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
- Tile resolution remains neutral metadata only and does not mutate `GameState`.
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
- Add end-turn, tile resolution, gameplay snapshots, or broader turn-action WebSocket slices only if explicitly assigned.

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
- Non-roll turn execution over network.
- Tile resolution or tile effects from WebSocket roll handling.
- WebSocket end-turn handling.
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

Yes. Chunk 5.4 is complete, and a fresh session should continue from this handover before starting the next assigned Phase 5 chunk.
