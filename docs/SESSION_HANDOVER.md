# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 3
- Chunk: 3.6 Anti-loop borrowing rules
- Completion status: Chunk 3.6 complete; borrowing is context-aware and loan-payment borrowing purposes are rejected before cash or loan state can mutate.
- Branch: `main` tracking `origin/main`; local has this chunk implemented and validated, not committed in this session.
- Previous commit: `654dec7` - `phase-3-3: add auction finalization`
- Last commit: `phase-3-4: add loan shark foundation`
- Date/time: 2026-04-27 00:32 +12:00

## Last Completed Chunk

Phase 3, Chunk 3.6 - Anti-loop borrowing rules only.

Completed:

- Added `BorrowPurpose` as the explicit context for borrowing.
- Changed `LoanManager.TakeLoan` to require a `BorrowPurpose`.
- `LoanTakeResult` now records the attempted borrow purpose.
- Added `LoanTakeResultKind.DisallowedBorrowPurpose`.
- Allows borrowing for `AuctionBid`, `RentPayment`, `TaxPayment`, `CardPenalty`, and `Fine`.
- Rejects borrowing for `LoanInterest`, `LoanPrincipalRepayment`, and `ExistingLoanDebt`.
- Rejected anti-loop borrow attempts return the unchanged `GameState`; player money and existing loan state are not mutated.
- Existing invalid amount, eliminated player, player-not-in-game, loan state, and interest escalation behavior remain unchanged apart from requiring a purpose argument.
- Added focused `LoanManagerTests` proving every allowed purpose is accepted and every blocked loan-payment purpose is rejected.

Not included by explicit user scope:

- Repayment system.
- Networking.
- Unity/UI.
- Persistence.
- Stats.

## Files Changed In This Chunk

- `server-dotnet/MonoJoey.Server/GameEngine/LoanManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/BorrowPurpose.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/LoanTakeResult.cs`
- `server-dotnet/MonoJoey.Server.Tests/GameEngine/LoanManagerTests.cs`
- `docs/SESSION_HANDOVER.md`

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
- `server-dotnet/MonoJoey.Server/GameEngine/DefaultBoardFactory.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DiceRoll.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DiceService.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/EliminationReason.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/GameState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/IDiceRoller.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/LoanManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/LoanTakeResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Money.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/MovementManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/MovementResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Player.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/PlayerEliminationResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/PlayerLoanState.cs`
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
  - Output summary: 97 tests passed.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `git status --short --branch`
  - Result: `main...origin/main` with modified Phase 3.6 files and this handover doc.

## Known Issues

- Plain `dotnet build .\server-dotnet\MonoJoey.sln` and plain `dotnet test .\server-dotnet\MonoJoey.sln` can fail in this Windows shell with no MSBuild errors once the server project participates in the solution graph.
- Serialized validation with `-m:1` succeeds and should be used unless the build harness is revisited.
- `NU1900` warnings remain because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json`.
- `AGENTS.md` and `LEAN-CTX.md` were not present at the repo root in this sandbox view, though instructions referenced them.

## Placeholders Introduced Or Preserved

- `AuctionConfig.InitialPreBidSeconds` defaults to `9`; still no real countdown loop exists.
- `AuctionConfig.BidResetSeconds` defaults to `3`; valid bids now copy this value into `AuctionState.CountdownDurationSeconds`, but no actual time passes.
- `AuctionConfig.MinimumBidIncrement` defaults to `1`; bid validation now uses it after the first bid.
- `AuctionConfig.StartingBid` defaults to `0`; first-bid validation now uses it.
- `AuctionStatus.ActiveBidCountdown` is metadata only; it does not trigger asynchronous behavior.
- `AuctionState.CountdownDurationSeconds` stores deterministic countdown duration metadata only.
- Placeholder board IDs/display names from Chunk 2.1 are preserved.
- Tile resolution action kinds remain placeholders and do not apply game effects.
- Property rent currently uses base rent only: the first rent table value, or a placeholder `10` for purchasable tiles without a rent table.
- Bankruptcy is hard elimination only; balances are not auto-corrected, no assets are liquidated, and no debt recovery is attempted.
- Loan interest is deducted only at turn start through `LoanManager.StartTurnInterestCheck`; it is not compounded, repaid, or otherwise collected.
- Loan interest after the third borrow increases by 10 percentage points per loan tier and caps at 100%.
- No protected Monopoly wording, branding, board names, card wording, artwork, or final token assumptions were introduced.

## Important Decisions Preserved

- Server-authoritative rules state.
- Unity remains untouched.
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
- Unpaid start-turn interest is a forced deduction; if the resulting balance is negative, existing negative-balance bankruptcy elimination marks the player bankrupt/eliminated.
- Loan enforcement does not interact with auctions, repayment, networking, UI, persistence, or stats.

## Next Recommended Chunk

Phase 3 follow-up - choose one narrow loan repayment or landing-integration slice, only if explicitly requested.

Possible next scopes:

- Hook unowned property landing resolution into `AuctionManager.StartMandatoryAuction`.
- Add active-auction storage to `GameState`, if needed before integration.
- Add repayment behavior only if explicitly assigned in a later chunk.

Recommended validation:

- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`
- `git status --short --branch`

## Do Not Touch Notes

Do not implement before its assigned chunk:

- Real wall-clock timers.
- Async countdown loop.
- Borrowing to cover auctions beyond the explicit `BorrowPurpose.AuctionBid` context marker.
- Loan repayment.
- Auction retry logic.
- Debt recovery.
- Asset liquidation.
- Cards.
- Mortgages.
- Houses/upgrades.
- Trading.
- Taxes/fines money changes.
- Jail/lockup status behavior.
- Lobbies.
- WebSockets.
- Database persistence.
- Stats.
- Unity scenes, prefabs, assets, project settings, metadata, animations, or editor UI.

## Fresh-Session Recommendation

Yes. Chunk 3.5 is complete, and a fresh session should continue from this handover before starting the next rules-engine chunk.
