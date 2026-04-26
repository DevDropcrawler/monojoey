# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 2
- Chunk: 2.5 tile resolution hooks
- Completion status: Chunk 2.5 complete; tile resolution result hooks implemented and ready for the next explicitly requested chunk.
- Branch: `main` tracking `origin/main`; local has this chunk staged/committed after final validation.
- Previous commit: `9c0e4e4` - `phase-2-4: add movement system`
- Commit: pending at handover write time; see `git log -1` after the Chunk 2.5 commit.
- Date/time: 2026-04-26 19:02 +12:00

## Last Completed Chunk

Phase 2, Chunk 2.5 - server-side tile resolution hooks only.

Completed:

- Added a server-side tile resolver for the player's current board tile.
- Added a neutral tile resolution result with player ID, tile ID, tile index, tile type, action requirement flag, no-action helper, and placeholder action kind.
- Added placeholder action categories for no action, start, property-like purchasable tiles, deck tiles, tax tiles, and go-to-lockup tiles.
- Added validation for unknown players and players whose current tile is not present on the board.
- Added focused tests for no-op tile resolution, property placeholder resolution, Start tile resolution, invalid player/tile handling, and non-mutation of money, ownership, player location, phase, turn number, and game end state.

Not included by explicit user scope:

- Rent.
- Buying property.
- Auctions.
- Loan Shark.
- Card draws or card effects.
- Jail/status logic.
- Taxes/fines money changes.
- Networking.
- Unity/UI.
- Stats.
- Persistence.

## Files Changed In This Chunk

- `server-dotnet/MonoJoey.Server/GameEngine/TileResolutionActionKind.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/TileResolutionResult.cs`
- `server-dotnet/MonoJoey.Server/GameEngine/TileResolver.cs`
- `server-dotnet/MonoJoey.Server.Tests/GameEngine/TileResolverTests.cs`
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
- `server-dotnet/MonoJoey.Server/GameEngine/RandomDiceRoller.cs`
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
  - Output summary: 28 tests passed.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `git status --short --branch`
  - Run after build/test and before final staging/commit as requested.

## Known Issues

- Plain `dotnet build .\server-dotnet\MonoJoey.sln` and plain `dotnet test .\server-dotnet\MonoJoey.sln` can fail in this Windows shell with no MSBuild errors once the server project participates in the solution graph.
- Serialized validation with `-m:1` succeeds and should be used for Phase 2 chunks unless the build harness is revisited.
- `NU1900` warnings remain because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json`.
- `LEAN-CTX.md` is referenced by `AGENTS.md` but was not present at the repo root in this sandbox view during this chunk.

## Placeholders Introduced Or Preserved

- Placeholder board IDs/display names from Chunk 2.1 are preserved.
- New tile resolution action kinds are placeholders only and do not apply game effects.
- No protected Monopoly wording, branding, board names, card wording, artwork, or final token assumptions were introduced.
- No deterministic production dice sequence was introduced; deterministic dice behavior remains represented by test injection through `IDiceRoller`.
- No ownership, rent, reward, jail/lockup status, auction, card, loan, tax/fine money, networking, Unity, stats, or persistence behavior was introduced.

## Important Decisions Preserved

- Server-authoritative rules state.
- Unity remains untouched.
- No networking, lobbies, stats, persistence, auctions, Loan Shark logic, property/rent behavior, card behavior, pass-start reward payment, lockup behavior, or bankruptcy behavior was added.
- Core game engine code lives under `server-dotnet/MonoJoey.Server/GameEngine`.
- Dice are server-owned through a service and injectable roller abstraction.
- Movement is deterministic and consumes an already-known step count; it does not roll dice or apply landing effects.
- Tile resolution currently returns neutral metadata only and does not mutate `GameState`.

## Next Recommended Chunk

Phase 2 follow-up - choose one narrow rules slice, only if explicitly requested.

Possible next scopes:

- Pass-start reward handling.
- Property purchase offer flow.
- End-turn transition after resolution.

Recommended validation:

- `dotnet build .\server-dotnet\MonoJoey.sln -m:1`
- `dotnet test .\server-dotnet\MonoJoey.sln -m:1`
- `git status --short --branch`

## Do Not Touch Notes

Do not implement before its assigned chunk:

- Property ownership or rent.
- Bankruptcy/elimination behavior.
- Auctions.
- Loan Shark.
- Cards.
- Taxes/fines money changes.
- Jail/lockup status behavior.
- Lobbies.
- WebSockets.
- Database persistence.
- Stats.
- Unity scenes, prefabs, assets, project settings, metadata, animations, or editor UI.

## Fresh-Session Recommendation

Yes. Chunk 2.5 is complete, and a fresh session should continue from this handover before starting the next rules-engine chunk.
