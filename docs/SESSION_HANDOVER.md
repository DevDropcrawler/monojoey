# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 1
- Chunk: 1.3 server test harness baseline cleanup
- Completion status: Completed.
- Branch: `main` (`origin/main` is 3 commits ahead of local; local is 2 commits ahead before this chunk commit)
- Previous commits:
  - `cac8157` - `phase-1-1: add dotnet server skeleton`
  - `eb9f674` - `phase-1-2: add shared contract placeholders`
- Commit: pending at handover write time; see `git log -1` after the chunk commit.
- Date/time: 2026-04-26 10:08 +12:00

## Last Completed Chunk

Phase 1, Chunk 1.3 - server test harness baseline cleanup.

Completed:

- Added `server-dotnet/MonoJoey.Server.Tests/README.md`.
- Documented test class and method naming conventions.
- Documented that future domain behavior tests belong with the chunks that introduce behavior.
- Confirmed no additional code cleanup was needed in this chunk.

Not implemented:

- Gameplay.
- Rules engine.
- Auctions.
- Loan Shark.
- Cards.
- Lobbies.
- WebSockets.
- Database.
- Stats.
- Unity project files, scenes, prefabs, assets, metadata, or animation systems.

## Files/Folders Created

- `server-dotnet/MonoJoey.Server.Tests/README.md`

## Files Changed

- `docs/SESSION_HANDOVER.md`

## Validation Commands Run

- `codex.cmd --version`
  - Result: succeeded.
  - Output: `codex-cli 0.125.0`
  - Note: emitted PATH update warning: `Access is denied. (os error 5)`.
- `dotnet --version`
  - Result: succeeded.
  - Output: `8.0.420`
- `git status --short --branch`
  - Result: succeeded.
  - Initial output: `## main...origin/main [ahead 2, behind 3]`.
- `dotnet build .\server-dotnet\MonoJoey.sln`
  - Result: succeeded.
  - Output summary: build succeeded, 2 warnings, 0 errors.
  - Warnings: `NU1900` vulnerability-data lookup could not reach `https://api.nuget.org/v3/index.json`.
- `dotnet test .\server-dotnet\MonoJoey.sln`
  - Result: succeeded.
  - Output summary: 2 tests passed, 0 failed, 0 skipped.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `git status --short --branch`
  - Result before handover update: succeeded.
  - Output: `## main...origin/main [ahead 2, behind 3]` plus untracked `server-dotnet/MonoJoey.Server.Tests/README.md`.

## Known Issues

- `MonoJoey.Server` is listed in the solution, but its `Build.0` entries remain disabled.
- `MonoJoey.Shared` is intentionally not listed in the solution after testing showed the same parallel MSBuild cancellation when it directly participated in solution builds.
- `MonoJoey.Shared` is still built by `dotnet build .\server-dotnet\MonoJoey.sln` through the test project reference and by `dotnet build .\server-dotnet\MonoJoey.Server\MonoJoey.Server.csproj` through the server project reference.
- Reason: in this Windows shell, direct multi-project solution builds consistently cancel parallel MSBuild child builds, sometimes with zero errors. Project-reference builds are stable.
- `NU1900` warnings appear because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json` from this environment. Package restore/build still succeeds from the local package cache.
- Local branch is behind `origin/main` by 3 commits. No pull was performed because the user did not request it.

## Placeholders Introduced Or Preserved

- Introduced no new code placeholders in this chunk.
- Preserved shared protocol/contracts placeholders only.
- Preserved README-only Unity client area in `client-unity/`.
- Preserved README-only tools area in `tools/`.
- No protected Monopoly wording, branding, board names, card wording, artwork, or final token assumptions were introduced.

## Important Decisions Preserved

- Unity PC client later.
- C#/.NET authoritative server.
- WebSockets V1 later.
- Server-owned rules state.
- Client remains presentation-only later.
- Mandatory auctions and Loan Shark mode remain future first-class toggleable systems.
- PostgreSQL remains later, after stable in-memory server core.

## Next Recommended Chunk

Continue Phase 1, Chunk 1.4: final Phase 1 audit.

Recommended scope:

- Verify repo structure.
- Verify build/test.
- Verify Unity remains README-only.
- Verify no Phase 2 gameplay/rules systems were introduced.
- Update this handover with final Phase 1 status.

## Do Not Touch Notes

Do not implement:

- Board logic
- Dice
- Movement
- Auctions
- Loan Shark
- Cards
- Lobbies
- WebSockets
- Database
- Stats
- Unity scenes, prefabs, assets, project settings, metadata, animations, or editor UI

## Fresh-Session Recommendation

Not required for the final Phase 1 audit if kept short.
