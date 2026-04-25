# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current Status

- Phase: 1
- Chunk: 1.4 final Phase 1 audit
- Completion status: Phase 1 complete.
- Branch: `main` (`origin/main` is 3 commits ahead of local; local is 3 commits ahead before this chunk commit)
- Previous commits:
  - `cac8157` - `phase-1-1: add dotnet server skeleton`
  - `eb9f674` - `phase-1-2: add shared contract placeholders`
  - `5c1618d` - `phase-1-3: document server test harness conventions`
- Commit: pending at handover write time; see `git log -1` after the final audit commit.
- Date/time: 2026-04-26 10:10 +12:00

## Last Completed Chunk

Phase 1, Chunk 1.4 - final Phase 1 audit.

Completed:

- Verified required Phase 1 repo structure exists.
- Verified `client-unity/` remains README-only.
- Verified `tools/` remains README-only.
- Verified no gameplay/rules engine implementation was introduced.
- Verified no lobbies, WebSockets, database, stats, Unity scenes, or animation systems were introduced.
- Re-ran build/test validation.
- Re-ran server project build validation because the server project is not directly built by the solution in this environment.

## Phase 1 Deliverables

- `server-dotnet/MonoJoey.sln`
- `server-dotnet/MonoJoey.Server/`
- `server-dotnet/MonoJoey.Server.Tests/`
- `shared/MonoJoey.Shared/`
- `client-unity/README.md`
- `tools/README.md`
- Root `.gitignore`
- Root `Directory.Build.props`
- Updated `docs/SESSION_HANDOVER.md`

## Files/Folders Created Across Phase 1

- `.gitignore`
- `Directory.Build.props`
- `server-dotnet/MonoJoey.sln`
- `server-dotnet/MonoJoey.Server/`
- `server-dotnet/MonoJoey.Server.Tests/`
- `server-dotnet/MonoJoey.Server.Tests/README.md`
- `shared/MonoJoey.Shared/`

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
  - Initial output: `## main...origin/main [ahead 3, behind 3]`.
- `rg --files server-dotnet shared client-unity tools -g '!**/bin/**' -g '!**/obj/**'`
  - Result: succeeded.
  - Confirmed only expected Phase 1 files.
- `rg -n "class .*Auction|class .*Loan|class .*Lobby|WebSocket|DbContext|Npgsql|MatchState|Dice|Rent|Bankrupt|CardResolver|TurnManager" server-dotnet shared -g '!**/bin/**' -g '!**/obj/**'`
  - Result: succeeded.
  - Findings were limited to expected shared enum names and a README naming example, not implementations.
- `dotnet build .\server-dotnet\MonoJoey.sln`
  - Result: succeeded.
  - Output summary: build succeeded, 2 warnings, 0 errors.
  - Warnings: `NU1900` vulnerability-data lookup could not reach `https://api.nuget.org/v3/index.json`.
- `dotnet test .\server-dotnet\MonoJoey.sln`
  - Result: succeeded.
  - Output summary: 2 tests passed, 0 failed, 0 skipped.
  - Warnings: same `NU1900` vulnerability-data lookup warning.
- `dotnet build .\server-dotnet\MonoJoey.Server\MonoJoey.Server.csproj`
  - Result: succeeded.
  - Output summary: `MonoJoey.Shared` and `MonoJoey.Server` built successfully, 0 warnings, 0 errors.
- `git status --short --branch`
  - Result before handover update: succeeded.
  - Output: `## main...origin/main [ahead 3, behind 3]`.

## Known Issues

- `MonoJoey.Server` is listed in the solution, but its `Build.0` entries remain disabled.
- `MonoJoey.Shared` is intentionally not listed in the solution after testing showed the same parallel MSBuild cancellation when it directly participated in solution builds.
- `MonoJoey.Shared` is still built by `dotnet build .\server-dotnet\MonoJoey.sln` through the test project reference and by `dotnet build .\server-dotnet\MonoJoey.Server\MonoJoey.Server.csproj` through the server project reference.
- Reason: in this Windows shell, direct multi-project solution builds consistently cancel parallel MSBuild child builds, sometimes with zero errors. Project-reference builds are stable.
- `NU1900` warnings appear because NuGet vulnerability metadata lookup cannot reach `https://api.nuget.org/v3/index.json` from this environment. Package restore/build still succeeds from the local package cache.
- Local branch is behind `origin/main` by 3 commits. No pull was performed because the user did not request it.

## Placeholders Introduced Or Preserved

- `ServerAssemblyMarker` is a server assembly placeholder only.
- Shared protocol/contracts are placeholders only.
- Baseline xUnit tests are smoke tests only.
- Unity remains README-only.
- Tools remain README-only.
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

Start Phase 2, Chunk 2.1 in a fresh Codex session.

Recommended first reads for Phase 2:

- `docs/README.md`
- `docs/AGENT_RULES.md`
- `docs/BUILD_PHASES.md`
- `docs/SESSION_HANDOVER.md`
- `docs/RULES_ENGINE.md`
- `docs/DATA_SCHEMAS.md`

Phase 2 should start with server-side state models only. Do not implement auctions, loans, cards, lobbies, WebSockets, database, stats, Unity scenes, or animation systems in Chunk 2.1.

## Do Not Touch Notes

Do not implement before its assigned chunk:

- Dice/movement resolution
- Auctions
- Loan Shark
- Cards
- Lobbies
- WebSockets
- Database
- Stats
- Unity scenes, prefabs, assets, project settings, metadata, animations, or editor UI

## Fresh-Session Recommendation

Yes. Phase 1 is complete; start a fresh Codex session before Phase 2 to keep context clean.
