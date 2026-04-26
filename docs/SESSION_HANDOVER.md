# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 5
- Chunk: 5.1 Game session model only
- Completion status: Chunk 5.1 complete; server-side sessions can be created, looked up, joined, and left, with connected-player metadata stored separately from the existing rules-engine `GameState`.
- Branch: `main` tracking `origin/main`; local has this chunk implemented and validated for commit.
- Previous commit: `phase-4-5: add lockup status system`
- Last commit after this chunk: `phase-5-1: add session model`
- Date/time: 2026-04-27 10:37 +12:00

## Last Completed Chunk

Phase 5, Chunk 5.1 - Game session model only.

Completed:

- Added `GameSession` with `SessionId`, connected `Players`, existing engine `GameState`, and `GameSessionStatus`.
- Added `GameSessionStatus` with `Lobby`, `InGame`, and `Finished`.
- Added `PlayerConnection` with `PlayerId`, placeholder `ConnectionId`, and lobby `IsReady`.
- Added `SessionManager.CreateSession()` to create a lobby session with a fresh session ID, default board, empty engine-player list, and lobby-phase `GameState`.
- Added `SessionManager.JoinSession(sessionId, player)` to append a connected player and return the updated session.
- Added `SessionManager.LeaveSession(sessionId, player)` to remove a connected player and return the updated session.
- Added `SessionManager.GetSession(sessionId)` to return a stored session or `null` when missing.
- Added duplicate-join handling keyed by `PlayerId`; a duplicate join is an idempotent no-op and does not replace the existing connection record.
- Added invalid-session handling: `GetSession` returns `null`, while `JoinSession` and `LeaveSession` throw `InvalidOperationException` with `Session not found.`
- Added focused tests for session creation, joining, leaving, player-list updates, duplicate joins, player-not-present leave no-op, and invalid-session behavior.

Not included by explicit user scope:

- Networking.
- Unity/UI.
- Persistence.
- Stats.
- WebSockets.
- Message handling.
- Turn execution over network.
- Scaling.

## Files Changed In This Chunk

- `server-dotnet/MonoJoey.Server/Sessions/GameSession.cs`
- `server-dotnet/MonoJoey.Server/Sessions/GameSessionStatus.cs`
- `server-dotnet/MonoJoey.Server/Sessions/PlayerConnection.cs`
- `server-dotnet/MonoJoey.Server/Sessions/SessionManager.cs`
- `server-dotnet/MonoJoey.Server.Tests/Sessions/SessionManagerTests.cs`
- `docs/SESSION_HANDOVER.md`

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
  - Output summary: 154 tests passed.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `git status --short --branch`
  - Result before commit: `main...origin/main` with new session model files, session manager tests, and this handover doc.

## Known Issues

- Plain `dotnet build .\server-dotnet\MonoJoey.sln` and plain `dotnet test .\server-dotnet\MonoJoey.sln` can fail in this Windows shell with no MSBuild errors once the server project participates in the solution graph.
- Serialized validation with `-m:1` succeeds and should be used unless the build harness is revisited.
- `NU1900` warnings remain because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json`.
- `AGENTS.md` and `LEAN-CTX.md` were not present at the repo root in this sandbox view, though instructions referenced them.

## Placeholders Introduced Or Preserved

- Session IDs are generated as GUID `N` strings and are not yet public lobby codes or persisted identifiers.
- `PlayerConnection.ConnectionId` is a string placeholder only; no transport, WebSocket, or reconnect protocol exists yet.
- `PlayerConnection.IsReady` is lobby metadata only; no ready-check transition starts a game yet.
- `GameSession.Players` tracks connected players only and is intentionally separate from `GameState.Players`; joining a session does not create an engine player yet.
- `SessionManager` is an in-memory manager only; it has no persistence, distributed storage, cleanup, scaling, networking, or async behavior.
- Duplicate joins are idempotent by `PlayerId`; they do not replace the existing connection ID or ready state yet.
- Leaving with a player not present in the session is a safe no-op.
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

Phase 5 follow-up - choose the next narrow server-session slice only if explicitly requested.

Possible next scopes:

- Add lobby-to-engine player mapping/start-game transition.
- Add a narrow message DTO layer without transport.
- Add WebSocket transport only when the networking chunk is explicitly assigned.

Recommended validation:

- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`
- `git status --short --branch`

## Do Not Touch Notes

Do not implement before its assigned chunk:

- Real wall-clock timers.
- Async countdown loop.
- Networking.
- WebSockets.
- Message handling.
- Turn execution over network.
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

Yes. Chunk 5.1 is complete, and a fresh session should continue from this handover before starting the next assigned Phase 5 chunk.
