# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 2
- Chunk: 2.3 dice system
- Completion status: Chunk 2.3 complete; dice-only scope implemented and ready for the next explicitly requested chunk.
- Branch: `main` tracking `origin/main`; local has this chunk staged/committed after final validation.
- Previous commit: `a653fab` - `phase-2-2: add turn order manager`
- Commit: pending at handover write time; see `git log -1` after the Chunk 2.3 commit.
- Date/time: 2026-04-26 17:21 +12:00

## Last Completed Chunk

Phase 2, Chunk 2.3 - server-side dice system only.

Completed:

- Added validated `DiceRoll` model for two standard six-sided dice.
- Added `Total` and `IsDouble` derived dice result values.
- Added injectable `IDiceRoller` abstraction.
- Added `RandomDiceRoller` default production roller.
- Added `DiceService` wrapper for server-owned dice rolling.
- Added focused tests for deterministic injection, totals, doubles, invalid values, and default random range.

Not included by explicit user scope:

- Movement.
- Pass-start reward handling.
- Tile resolution.
- Rent or ownership behavior.
- Auctions.
- Loan Shark.
- Cards.
- Networking, Unity, stats, or persistence.

## Files Changed In This Chunk

- `server-dotnet/MonoJoey.Server/GameEngine/DiceRoll.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/IDiceRoller.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/RandomDiceRoller.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DiceService.cs`
- `server-dotnet/MonoJoey.Server.Tests/GameEngine/DiceServiceTests.cs`
- `docs/SESSION_HANDOVER.md`

## Existing Phase 2 Engine Files

- `server-dotnet/MonoJoey.Server/GameEngine/Board.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DefaultBoardFactory.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DiceRoll.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DiceService.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/GameState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/IDiceRoller.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Money.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Player.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/RandomDiceRoller.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Tile.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/TurnManager.cs`

## Validation Commands Run

- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
  - Result: succeeded.
  - Output summary: build succeeded, 2 warnings, 0 errors.
  - Warnings: `NU1900` vulnerability-data lookup could not reach `https://api.nuget.org/v3/index.json`.
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`
  - Result: succeeded.
  - Output summary: 16 tests passed.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `rg -n "Move|Moved|Rent|Loan|Auction|Card|TileResolved|CurrentTileId|OwnedProperty|DbContext|WebSocket|Lobby|Unity|Bankrupt" server-dotnet\MonoJoey.Server\GameEngine server-dotnet\MonoJoey.Server.Tests\GameEngine`
  - Result: only existing passive model/config fields, board placeholders, and test setup values were found; no out-of-scope behavior was added.
- `git status --short --branch`
  - Run before final staging/commit and after final validation as requested.

## Known Issues

- Plain `dotnet build .\server-dotnet\MonoJoey.sln` and plain `dotnet test .\server-dotnet\MonoJoey.sln` can fail in this Windows shell with no MSBuild errors once the server project participates in the solution graph.
- Serialized validation with `-m:1` succeeds and should be used for Phase 2 chunks unless the build harness is revisited.
- `NU1900` warnings remain because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json`.
- `LEAN-CTX.md` is referenced by `AGENTS.md` but was not present at the repo root in this sandbox view during this chunk.

## Placeholders Introduced Or Preserved

- Placeholder board IDs/display names from Chunk 2.1 are preserved.
- No protected Monopoly wording, branding, board names, card wording, artwork, or final token assumptions were introduced.
- No deterministic production dice sequence was introduced; deterministic dice behavior is represented by test injection through `IDiceRoller`.

## Important Decisions Preserved

- Server-authoritative rules state.
- Unity remains untouched.
- No networking, lobbies, stats, persistence, auctions, Loan Shark logic, movement, tile-resolution behavior, property/rent behavior, card behavior, or bankruptcy behavior was added.
- Core game engine code lives under `server-dotnet/MonoJoey.Server/GameEngine`.
- Dice are server-owned through a service and injectable roller abstraction.

## Next Recommended Chunk

Phase 2 follow-up - movement resolution, if the plan continues splitting the original 2.3 dice/movement chunk.

Recommended scope:

- Move the current player by an already server-rolled dice total.
- Wrap around the board.
- Apply pass-start reward if the design calls for it in that chunk.
- Keep tile resolution, rent, auctions, cards, loans, networking, Unity, stats, and persistence out of scope unless explicitly requested.

Recommended validation:

- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`
- `git status --short --branch`

## Do Not Touch Notes

Do not implement before its assigned chunk:

- Movement resolution, unless explicitly requested as the next chunk.
- Tile resolution behavior.
- Property ownership or rent.
- Bankruptcy/elimination behavior.
- Auctions.
- Loan Shark.
- Cards.
- Lobbies.
- WebSockets.
- Database persistence.
- Stats.
- Unity scenes, prefabs, assets, project settings, metadata, animations, or editor UI.

## Fresh-Session Recommendation

Yes. Chunk 2.3 is complete, and a fresh session should continue from this handover before starting the next movement or rules-engine chunk.
