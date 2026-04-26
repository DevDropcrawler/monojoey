# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 3
- Chunk: 3.1 mandatory auction foundation
- Completion status: Chunk 3.1 complete; server-side auction config, state models, no-start results, and mandatory auction start validation are implemented.
- Branch: `main` tracking `origin/main`; local has this chunk committed after final validation.
- Previous commit: `5c0fdfd` - `phase-2-7: add bankruptcy and elimination`
- Commit: `phase-3-1: add auction foundation`
- Date/time: 2026-04-26 23:44 +12:00

## Last Completed Chunk

Phase 3, Chunk 3.1 - mandatory auction foundation only.

Completed:

- Added `AuctionConfig` with mandatory-auction enablement plus placeholder timer and bid config values.
- Added `AuctionState`, `AuctionStatus`, and `AuctionBid` as the server-side auction domain surface.
- Added `AuctionStartResult` and `AuctionStartResultKind` so the manager can return clear no-auction outcomes.
- Added `AuctionManager.StartMandatoryAuction` for creating an initial auction state for an enabled, unowned, auctionable property tile.
- Added validation for unknown triggering players and unknown auction tiles.
- Added no-auction outcomes for disabled mandatory auctions, already-owned properties, and non-auctionable/non-property tiles.
- Added focused tests for default config values, valid auction start, disabled auctions, owned property, non-property tile, unknown player, and unknown tile.

Not included by explicit user scope:

- Timer countdown logic.
- 9-second pre-bid timer behavior.
- 3-second bid reset timer behavior.
- Bid placement or bid validation.
- Auction resolution.
- Winner payment or property transfer.
- Loan Shark.
- Networking.
- Unity/UI.
- Persistence.
- Stats.

## Files Changed In This Chunk

- `server-dotnet/MonoJoey.Server/GameEngine/AuctionBid.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionConfig.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionStartResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionStatus.cs`
- `server-dotnet/MonoJoey.Server.Tests/GameEngine/AuctionManagerTests.cs`
- `docs/SESSION_HANDOVER.md`

## Existing Engine Files

- `server-dotnet/MonoJoey.Server/GameEngine/AuctionBid.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionConfig.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionStartResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/AuctionStatus.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/BankruptcyManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Board.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DefaultBoardFactory.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DiceRoll.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DiceService.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/EliminationReason.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/GameState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/IDiceRoller.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Money.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/MovementManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/MovementResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Player.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/PlayerEliminationResult.cs`
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
  - Output summary: 57 tests passed.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `git status --short --branch`
  - Run after build/test and before final staging/commit as requested.
  - Output showed `main...origin/main` with seven new auction files untracked.

## Known Issues

- Plain `dotnet build .\server-dotnet\MonoJoey.sln` and plain `dotnet test .\server-dotnet\MonoJoey.sln` can fail in this Windows shell with no MSBuild errors once the server project participates in the solution graph.
- Serialized validation with `-m:1` succeeds and should be used unless the build harness is revisited.
- `NU1900` warnings remain because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json`.
- `AGENTS.md` and `LEAN-CTX.md` were not present at the repo root in this sandbox view, though instructions referenced them.

## Placeholders Introduced Or Preserved

- `AuctionConfig.InitialPreBidSeconds` defaults to `9`, but no countdown behavior exists yet.
- `AuctionConfig.BidResetSeconds` defaults to `3`, but no bid reset behavior exists yet.
- `AuctionConfig.MinimumBidIncrement` defaults to `1`, but no bid placement validation exists yet.
- `AuctionConfig.StartingBid` defaults to `0`, but no bidding or resolution behavior exists yet.
- `AuctionState.Bids` starts empty and is not mutated by the foundation manager.
- `AuctionStatus.AwaitingInitialBid` is the only auction status currently used.
- Placeholder board IDs/display names from Chunk 2.1 are preserved.
- Tile resolution action kinds remain placeholders and do not apply game effects.
- Property rent currently uses base rent only: the first rent table value, or a placeholder `10` for purchasable tiles without a rent table.
- Bankruptcy is hard elimination only; balances are not auto-corrected, no assets are liquidated, and no debt recovery is attempted.
- No protected Monopoly wording, branding, board names, card wording, artwork, or final token assumptions were introduced.

## Important Decisions Preserved

- Server-authoritative rules state.
- Unity remains untouched.
- Core game engine code lives under `server-dotnet/MonoJoey.Server/GameEngine`.
- Auctions currently produce standalone `AuctionState`; `GameState` is not mutated and does not yet store active auction state.
- `AuctionManager.StartMandatoryAuction` treats `Tile.IsPurchasable && Tile.IsAuctionable` as auction eligibility.
- Disabled mandatory auctions return a typed no-auction result before player/tile lookup.
- Owned properties and non-auctionable tiles return typed no-auction results instead of throwing.
- Unknown triggering players and unknown tiles throw `InvalidOperationException`, consistent with existing manager validation style.
- Ownership continues to live on `Player.OwnedPropertyIds`; no new persistence or aggregate ownership store was added.
- `PropertyManager.AssignOwner` remains the future ownership hook for auction resolution, but this chunk does not call it.
- Tile resolution remains neutral metadata only and does not mutate `GameState`.
- Dice are server-owned through a service and injectable roller abstraction.
- Movement is deterministic and consumes an already-known step count; it does not roll dice or apply landing effects.

## Next Recommended Chunk

Phase 3 follow-up - choose one narrow auction slice, only if explicitly requested.

Possible next scopes:

- Hook unowned property landing resolution into `AuctionManager.StartMandatoryAuction`.
- Add bid placement validation without timers.
- Add auction resolution without payment/transfer, if staged separately.
- Add winner payment and property transfer after bid validation exists.

Recommended validation:

- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`
- `git status --short --branch`

## Do Not Touch Notes

Do not implement before its assigned chunk:

- Timer countdown logic.
- 9-second pre-bid timer behavior.
- 3-second bid reset timer behavior.
- Bid placement unless explicitly assigned.
- Auction resolution unless explicitly assigned.
- Winner payment/property transfer unless explicitly assigned.
- Loan Shark.
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

Yes. Chunk 3.1 is complete, and a fresh session should continue from this handover before starting the next rules-engine chunk.
