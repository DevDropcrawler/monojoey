# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 2
- Chunk: 2.6 basic property system
- Completion status: Chunk 2.6 complete; basic server-side property ownership, purchase, and rent transfer helpers are implemented.
- Branch: `main` tracking `origin/main`; local has this chunk staged/committed after final validation.
- Previous commit: `cfeac20` - `phase-2-5: add tile resolution hooks`
- Commit: pending at handover write time; see `git log -1` after the Chunk 2.6 commit.
- Date/time: 2026-04-26 19:18 +12:00

## Last Completed Chunk

Phase 2, Chunk 2.6 - basic property system only.

Completed:

- Added `PropertyManager` for server-side property ownership and rent behavior.
- Added `AssignOwner` for assigning an unowned purchasable tile to an existing player without money transfer.
- Added `BuyProperty` for buying an unowned purchasable tile, deducting the tile price, and assigning ownership.
- Added `PayRentForCurrentTile` for paying rent from the landing player to the owner of the landed purchasable tile.
- Rent uses the first value from the tile rent table, with a simple placeholder fallback for purchasable tiles that have no rent table.
- Added result records for property purchases and rent payments.
- Added validation for unknown owners, buyers, landing players, missing property tiles, non-purchasable tiles, already-owned properties, duplicate ownership, and rent that would make the landing player negative.
- Added focused tests for assignment/purchase, rent charging, rent transfer, own-property no-charge, unowned-property no-charge, non-property no-rent, and invalid handling.

Not included by explicit user scope:

- Auctions or mandatory auction flow.
- Loan Shark.
- Cards.
- Mortgages.
- Houses/upgrades.
- Trading.
- Bankruptcy or elimination flow.
- Networking.
- Unity/UI.
- Stats.
- Persistence.

## Files Changed In This Chunk

- `server-dotnet/MonoJoey.Server/GameEngine/PropertyManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/PropertyPurchaseResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/RentPaymentResult.cs`
- `server-dotnet/MonoJoey.Server.Tests/GameEngine/PropertyManagerTests.cs`
- `docs/SESSION_HANDOVER.md`

## Existing Phase 2 Engine Files

- `server-dotnet/MonoJoey.Server/GameEngine/Board.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DefaultBoardFactory.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DiceRoll.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DiceService.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/GameState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/IDiceRoller.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Money.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/MovementManager.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/MovementResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Player.cs`
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
  - Output summary: 41 tests passed.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `git status --short --branch`
  - Run after build/test and before final staging/commit as requested.
  - Output showed `main...origin/main` with the new property-system files and handover update untracked/modified before staging.

## Known Issues

- Plain `dotnet build .\server-dotnet\MonoJoey.sln` and plain `dotnet test .\server-dotnet\MonoJoey.sln` can fail in this Windows shell with no MSBuild errors once the server project participates in the solution graph.
- Serialized validation with `-m:1` succeeds and should be used for Phase 2 chunks unless the build harness is revisited.
- `NU1900` warnings remain because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json`.
- `LEAN-CTX.md` is referenced by `AGENTS.md` but was not present at the repo root in this sandbox view during this chunk.

## Placeholders Introduced Or Preserved

- Placeholder board IDs/display names from Chunk 2.1 are preserved.
- Tile resolution action kinds remain placeholders and do not apply game effects.
- Property rent currently uses base rent only: the first rent table value, or a placeholder `10` for purchasable tiles without a rent table.
- No protected Monopoly wording, branding, board names, card wording, artwork, or final token assumptions were introduced.
- No deterministic production dice sequence was introduced; deterministic dice behavior remains represented by test injection through `IDiceRoller`.
- No auction, Loan Shark, card, mortgage, house/upgrade, trade, tax/fine money, networking, Unity, stats, or persistence behavior was introduced.

## Important Decisions Preserved

- Server-authoritative rules state.
- Unity remains untouched.
- Core game engine code lives under `server-dotnet/MonoJoey.Server/GameEngine`.
- Ownership continues to live on `Player.OwnedPropertyIds`; no new persistence or aggregate ownership store was added.
- `PropertyManager.AssignOwner` is intentionally narrow so a future auction flow can assign ownership after its own bidding rules.
- `PropertyManager.BuyProperty` only buys unowned purchasable tiles and rejects purchases that would make the buyer negative.
- Rent payment is a focused money transfer and rejects rent that would make the landing player negative; bankruptcy/elimination is still not implemented.
- Tile resolution remains neutral metadata only and does not mutate `GameState`.
- Dice are server-owned through a service and injectable roller abstraction.
- Movement is deterministic and consumes an already-known step count; it does not roll dice or apply landing effects.

## Next Recommended Chunk

Phase 2 follow-up - choose one narrow rules slice, only if explicitly requested.

Possible next scopes:

- Pass-start reward handling.
- End-turn transition after resolution.
- Purchase offer state that calls into the basic property helper.

Recommended validation:

- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`
- `git status --short --branch`

## Do Not Touch Notes

Do not implement before its assigned chunk:

- Bankruptcy/elimination behavior.
- Auctions.
- Loan Shark.
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

Yes. Chunk 2.6 is complete, and a fresh session should continue from this handover before starting the next rules-engine chunk.
