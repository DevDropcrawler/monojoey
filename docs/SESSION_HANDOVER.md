# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 2
- Chunk: 2.2 turn system
- Completion status: Chunk 2.2 complete; ready for Chunk 2.3 dice system.
- Branch: `main` tracking `origin/main`; local is ahead by 1 before this chunk commit.
- Previous commit: `421203f` - `phase-2-1: add core game state models`
- Commit: pending at handover write time; see `git log -1` after the Chunk 2.2 commit.
- Date/time: 2026-04-26 17:01 +12:00

## Last Completed Chunk

Phase 2, Chunk 2.2 - minimal turn system.

Completed:

- Added `TurnManager` for server-side turn order only.
- Implemented first-turn selection.
- Implemented current-player lookup.
- Implemented next-turn advancement in player-list order.
- Implemented wrap-around from the last player back to the first player.
- Added focused tests for first turn, next turn, wrap-around, and current-player lookup.

## Files Changed In This Chunk

- `server-dotnet/MonoJoey.Server/GameEngine/TurnManager.cs`
- `server-dotnet/MonoJoey.Server.Tests/GameEngine/TurnManagerTests.cs`
- `docs/SESSION_HANDOVER.md`

## Existing Phase 2 Engine Files

- `server-dotnet/MonoJoey.Server/GameEngine/Board.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DefaultBoardFactory.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/GameState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Money.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Player.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Tile.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/TurnManager.cs`

## Validation Commands Run

- `codex.cmd --version`
  - Result: succeeded.
  - Output: `codex-cli 0.125.0`
  - Note: emitted PATH update warning: `Access is denied. (os error 5)`.
- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
  - Preflight result before edits: succeeded.
  - Post-implementation result: succeeded.
  - Output summary: build succeeded, 2 warnings, 0 errors.
  - Warnings: `NU1900` vulnerability-data lookup could not reach `https://api.nuget.org/v3/index.json`.
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`
  - Preflight result before edits: succeeded, 5 tests passed.
  - Post-implementation result: succeeded, 9 tests passed.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `rg -n "Dice|Move|Rent|Loan|WebSocket|DbContext|Lobby|Unity|Bankrupt|Auction" server-dotnet\MonoJoey.Server\GameEngine server-dotnet\MonoJoey.Server.Tests\GameEngine`
  - Result: only passive model/config fields and test setup values were found; no out-of-scope behavior was added.

## Known Issues

- Plain `dotnet build .\server-dotnet\MonoJoey.sln` and plain `dotnet test .\server-dotnet\MonoJoey.sln` can fail in this Windows shell with no MSBuild errors once the server project participates in the solution graph.
- Serialized validation with `-m:1` succeeds and should be used for Phase 2 chunks unless the build harness is revisited.
- `NU1900` warnings remain because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json`.

## Placeholders Introduced Or Preserved

- Placeholder board IDs/display names from Chunk 2.1 are preserved.
- No protected Monopoly wording, branding, board names, card wording, artwork, or final token assumptions were introduced.
- `TurnManager` does not skip bankrupt players yet; bankruptcy/elimination remains a later chunk.

## Important Decisions Preserved

- Server-authoritative rules state.
- Unity remains untouched.
- No networking, lobbies, stats, persistence, auctions, Loan Shark logic, dice, movement, tile-resolution behavior, property/rent behavior, or bankruptcy behavior was added.
- Core game engine code lives under `server-dotnet/MonoJoey.Server/GameEngine`.

## Next Recommended Chunk

Phase 2, Chunk 2.3 - dice system.

Recommended scope:

- Add server-owned dice roll abstraction.
- Add deterministic injectable dice/random source for tests.
- Add dice range tests.
- Do not move players yet unless the user explicitly combines dice and movement; movement is the next separate chunk in the current user plan.

Recommended validation:

- `codex.cmd --version`
- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`

## Do Not Touch Notes

Do not implement before its assigned chunk:

- Movement resolution
- Tile resolution behavior
- Property ownership or rent
- Bankruptcy/elimination behavior
- Auctions
- Loan Shark
- Cards
- Lobbies
- WebSockets
- Database persistence
- Stats
- Unity scenes, prefabs, assets, project settings, metadata, animations, or editor UI

## Fresh-Session Recommendation

Yes. Two Phase 2 chunks are now complete, and a fresh session should continue from this handover before starting Chunk 2.3.
