# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 1
- Chunk: 1.2 lightweight shared contracts/protocol placeholder
- Completion status: Completed with documented solution-build environment caveats.
- Branch: `main` (`origin/main` is 3 commits ahead of local; local is 1 commit ahead before this chunk commit)
- Previous commit: `cac8157` (`phase-1-1: add dotnet server skeleton`)
- Commit: pending at handover write time; see `git log -1` after the chunk commit.
- Date/time: 2026-04-26 10:01 +12:00

## Last Completed Chunk

Phase 1, Chunk 1.2 - lightweight shared contracts/protocol placeholder.

Completed:

- Created `shared/MonoJoey.Shared/` as a .NET 8 class library.
- Added lightweight protocol ID wrappers.
- Added client message, server event, and error-code enums from the protocol docs.
- Added schema placeholder enums for game phase, tile type, card action type, and money reason.
- Added a minimal player profile DTO.
- Added project references from server and tests to shared.
- Added a baseline shared-contract reference test.
- Updated `shared/README.md`.

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

- `shared/MonoJoey.Shared/`
- `shared/MonoJoey.Shared/MonoJoey.Shared.csproj`
- `shared/MonoJoey.Shared/Protocol/Identifiers.cs`
- `shared/MonoJoey.Shared/Protocol/MessageTypes.cs`
- `shared/MonoJoey.Shared/Protocol/Envelopes.cs`
- `shared/MonoJoey.Shared/Schemas/SchemaEnums.cs`
- `shared/MonoJoey.Shared/Dtos/PlayerProfileSelectionDto.cs`

## Files Changed

- `shared/README.md`
- `server-dotnet/MonoJoey.Server/MonoJoey.Server.csproj`
- `server-dotnet/MonoJoey.Server.Tests/MonoJoey.Server.Tests.csproj`
- `server-dotnet/MonoJoey.Server.Tests/BaselineTests.cs`
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
  - Initial output: `## main...origin/main [ahead 1, behind 3]`.
- `dotnet build .\server-dotnet\MonoJoey.sln`
  - Result: succeeded.
  - Output summary: build succeeded, 2 warnings, 0 errors.
  - Confirmed `MonoJoey.Shared` builds via the test project reference.
  - Warnings: `NU1900` vulnerability-data lookup could not reach `https://api.nuget.org/v3/index.json`.
- `dotnet test .\server-dotnet\MonoJoey.sln`
  - Result: succeeded.
  - Output summary: 2 tests passed, 0 failed, 0 skipped.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `dotnet build .\server-dotnet\MonoJoey.Server\MonoJoey.Server.csproj`
  - Result: succeeded.
  - Output summary: `MonoJoey.Shared` and `MonoJoey.Server` built successfully, 0 warnings, 0 errors.
- `dotnet sln .\server-dotnet\MonoJoey.sln list`
  - Result: succeeded.
  - Projects listed:

```text
MonoJoey.Server.Tests\MonoJoey.Server.Tests.csproj
MonoJoey.Server\MonoJoey.Server.csproj
```

## Known Issues

- `MonoJoey.Server` is listed in the solution, but its `Build.0` entries remain disabled.
- `MonoJoey.Shared` is intentionally not listed in the solution after testing showed the same parallel MSBuild cancellation when it directly participated in solution builds.
- `MonoJoey.Shared` is still built by `dotnet build .\server-dotnet\MonoJoey.sln` through the test project reference and by `dotnet build .\server-dotnet\MonoJoey.Server\MonoJoey.Server.csproj` through the server project reference.
- Reason: in this Windows shell, direct multi-project solution builds consistently cancel parallel MSBuild child builds, sometimes with zero errors. Project-reference builds are stable.
- `NU1900` warnings appear because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json` from this environment. Package restore/build still succeeds from the local package cache.
- Local branch is behind `origin/main` by 3 commits. No pull was performed because the user did not request it.

## Placeholders Introduced Or Preserved

- Introduced shared protocol/contracts placeholders only.
- Introduced no validation logic, rules logic, gameplay state, or behavior.
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

Continue Phase 1, Chunk 1.3: baseline server/test harness cleanup only if still needed.

Read only:

- `docs/TECH_ARCHITECTURE.md`
- `docs/AGENT_RULES.md`
- `docs/SESSION_HANDOVER.md`

Recommended scope:

- Audit whether Chunk 1.3 has anything left after the baseline tests added in Chunks 1.1 and 1.2.
- If no code cleanup is needed, update handover and proceed to final Phase 1 audit.
- Do not add gameplay or rules behavior.

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

Not required yet. Context risk is moderate but still manageable for a short Chunk 1.3 audit.
