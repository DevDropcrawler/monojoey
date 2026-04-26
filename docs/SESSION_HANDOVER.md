# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 2
- Chunk: 2.1 core domain models
- Completion status: Chunk 2.1 complete; ready for Chunk 2.2 turn system.
- Branch: `main` tracking `origin/main`
- Previous commit: `9c81b54` - `phase-1-4: complete phase 1 audit`
- Commit: pending at handover write time; see `git log -1` after the Chunk 2.1 commit.
- Date/time: 2026-04-26 16:51 +12:00

## Last Completed Chunk

Phase 2, Chunk 2.1 - core server-side domain models.

Completed:

- Added minimal server-side game engine model records:
  - `GameState`
  - `Player`
  - `Board`
  - `Tile`
  - `Money`
- Added `DefaultBoardFactory` with placeholder board data only.
- Added tests for default board validity:
  - board ID and start tile at index 0
  - unique tile IDs
  - sequential tile indexes
  - purchasable placeholder tiles have prices and auctionable flags
- Re-enabled `MonoJoey.Server` as a solution build participant so solution validation compiles the new server code.
- Updated the test project to reference `MonoJoey.Server`; shared contracts are consumed transitively through the server project.

## Files Changed

- `Directory.Build.props`
- `server-dotnet/MonoJoey.sln`
- `server-dotnet/MonoJoey.Server/GameEngine/Board.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/DefaultBoardFactory.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/GameState.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Money.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Player.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/Tile.cs`
- `server-dotnet/MonoJoey.Server.Tests/MonoJoey.Server.Tests.csproj`
- `server-dotnet/MonoJoey.Server.Tests/GameEngine/DefaultBoardFactoryTests.cs`
- `docs/SESSION_HANDOVER.md`

## Validation Commands Run

- `codex.cmd --version`
  - Result: succeeded.
  - Output: `codex-cli 0.125.0`
  - Note: emitted PATH update warning: `Access is denied. (os error 5)`.
- `dotnet build .\server-dotnet\MonoJoey.sln`
  - Initial result: failed due a self-inflicted parallel build/test file lock when preflight commands were started at the same time.
  - Sequential retry before edits: succeeded with 2 `NU1900` warnings.
- `dotnet test .\server-dotnet\MonoJoey.sln`
  - Preflight result before edits: succeeded, 2 tests passed.
- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
  - Result after edits: succeeded.
  - Output summary: build succeeded, 2 warnings, 0 errors.
  - Warnings: `NU1900` vulnerability-data lookup could not reach `https://api.nuget.org/v3/index.json`.
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`
  - Result after edits: succeeded.
  - Output summary: 5 tests passed, 0 failed, 0 skipped.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `rg -n "Auction|Loan|WebSocket|DbContext|Lobby|Unity|Dice|Move" server-dotnet\MonoJoey.Server\GameEngine server-dotnet\MonoJoey.Server.Tests\GameEngine`
  - Result: only `IsAuctionable` model/config fields were found; no auction logic, loan logic, networking, database, UI, dice, or movement code was added.

## Known Issues

- Plain `dotnet build .\server-dotnet\MonoJoey.sln` and plain `dotnet test .\server-dotnet\MonoJoey.sln` can fail in this Windows shell with no MSBuild errors once the server project participates in the solution graph.
- Serialized validation with `-m:1` succeeds and should be used for Phase 2 chunks unless the build harness is revisited.
- `Directory.Build.props` now sets `BuildInParallel=false` to reduce MSBuild project-reference contention, but the command-line `-m:1` flag is still required in this environment.
- `NU1900` warnings remain because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json`.

## Placeholders Introduced Or Preserved

- `DefaultBoardFactory` uses placeholder IDs and display names only.
- No protected Monopoly wording, branding, board names, card wording, artwork, or final token assumptions were introduced.
- `IsAuctionable` is a passive tile config flag only; no auction system or auction behavior was implemented.

## Important Decisions Preserved

- Server-authoritative rules state.
- Unity remains untouched.
- No networking, lobbies, stats, persistence, auctions, Loan Shark logic, dice, movement, or tile-resolution behavior was added.
- Core game engine code lives under `server-dotnet/MonoJoey.Server/GameEngine`.

## Next Recommended Chunk

Phase 2, Chunk 2.2 - turn system.

Recommended scope:

- Add current player / turn order state behavior.
- Add next-turn logic.
- Skip no players yet unless the chunk explicitly handles inactive/bankrupt placeholders.
- Keep dice, movement, tile resolution, property/rent, auctions, Loan Shark, networking, UI, stats, and persistence out of scope.

Recommended validation:

- `codex.cmd --version`
- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`

## Do Not Touch Notes

Do not implement before its assigned chunk:

- Dice or movement resolution
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

No. Context is still manageable after Chunk 2.1, but stop after Chunk 2.2 if the turn system expands beyond the intended small scope.
