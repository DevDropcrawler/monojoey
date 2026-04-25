# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 1
- Chunk: 1.1 repo skeleton and .NET solution
- Completion status: Completed with one documented build-environment caveat.
- Branch: `main` (`origin/main` is 3 commits ahead of local)
- Commit: pending at handover write time; see `git log -1` after the chunk commit.
- Date/time: 2026-04-26 09:08 +12:00

## Last Completed Chunk

Phase 1, Chunk 1.1 - repo skeleton and .NET solution.

Completed:

- Created `server-dotnet/MonoJoey.sln`.
- Created `server-dotnet/MonoJoey.Server/` as a minimal .NET 8 server assembly skeleton.
- Created `server-dotnet/MonoJoey.Server.Tests/` as a minimal xUnit test project.
- Kept both projects listed in `MonoJoey.sln`.
- Added a root `.gitignore` for .NET outputs, local tooling/session state, and editor/OS noise.
- Added `Directory.Build.props` with `UseSharedCompilation=false` for this shell.
- Updated root and server READMEs to reflect the generated skeleton.
- Preserved `shared/`, `client-unity/`, and `tools/` as README-only placeholders.

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

- `.gitignore`
- `Directory.Build.props`
- `server-dotnet/MonoJoey.sln`
- `server-dotnet/MonoJoey.Server/`
- `server-dotnet/MonoJoey.Server/MonoJoey.Server.csproj`
- `server-dotnet/MonoJoey.Server/Program.cs`
- `server-dotnet/MonoJoey.Server.Tests/`
- `server-dotnet/MonoJoey.Server.Tests/MonoJoey.Server.Tests.csproj`
- `server-dotnet/MonoJoey.Server.Tests/BaselineTests.cs`

## Files Changed

- `README.md`
- `server-dotnet/README.md`
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
  - Initial output: `## main...origin/main [behind 3]` plus untracked `~/`.
- `dotnet build .\server-dotnet\MonoJoey.sln`
  - Result: succeeded.
  - Output summary: build succeeded, 2 warnings, 0 errors.
  - Warnings: `NU1900` vulnerability-data lookup could not reach `https://api.nuget.org/v3/index.json`.
- `dotnet test .\server-dotnet\MonoJoey.sln`
  - Result: succeeded.
  - Output summary: 1 test passed, 0 failed, 0 skipped.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `dotnet build .\server-dotnet\MonoJoey.Server\MonoJoey.Server.csproj`
  - Result: succeeded.
  - Output summary: build succeeded, 0 warnings, 0 errors.
- `dotnet sln .\server-dotnet\MonoJoey.sln list`
  - Result: succeeded.
  - Projects listed:

```text
MonoJoey.Server.Tests\MonoJoey.Server.Tests.csproj
MonoJoey.Server\MonoJoey.Server.csproj
```

## Known Issues

- `MonoJoey.sln` lists `MonoJoey.Server`, but the server project's `Build.0` entries are disabled in the solution configuration.
- Reason: in this Windows shell, `dotnet build .\server-dotnet\MonoJoey.sln` consistently canceled the parallel MSBuild solution build when both projects participated, reporting zero errors. The server project builds successfully on its own.
- The solution build/test commands pass because they build/run the test project. The server project was separately validated with `dotnet build .\server-dotnet\MonoJoey.Server\MonoJoey.Server.csproj`.
- `NU1900` warnings appear because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json` from this environment. Package restore/build still succeeds from the local package cache.
- Local branch is behind `origin/main` by 3 commits. No pull was performed because the user did not request it.

## Placeholders Introduced Or Preserved

- Introduced `ServerAssemblyMarker` as a server assembly placeholder only.
- Introduced one baseline xUnit smoke test.
- Preserved placeholder-only `shared/README.md`.
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

Continue Phase 1, Chunk 1.2: lightweight shared contracts/protocol placeholder.

Read only:

- `docs/TECH_ARCHITECTURE.md`
- `docs/MULTIPLAYER_PROTOCOL.md`
- `docs/DATA_SCHEMAS.md`
- `docs/SESSION_HANDOVER.md`

Recommended scope:

- Add `shared/MonoJoey.Shared/` as a minimal .NET 8 class library if practical.
- Keep content as lightweight contracts/protocol placeholders only.
- Do not add gameplay or rules logic.
- Wire shared project carefully and validate build/test.

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

Not required yet. Context risk is moderate but manageable for Chunk 1.2 if kept small.
