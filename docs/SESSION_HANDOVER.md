# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current status

- Phase: 1
- Chunk: 1.1 repo skeleton and .NET solution
- Completion status: Placeholder-only bootstrap completed; .NET solution/projects were not generated because the .NET SDK is unavailable in this shell.
- Branch: `main`
- Commit: final chunk commit created after this handover update; see `git log -1` / final response for exact hash.
- Date/time: 2026-04-26 06:47 local

## Last completed chunk

Phase 1, Chunk 1.1 — repo skeleton placeholder path only.

Completed within the SDK-missing fallback rules:

- Created required top-level skeleton folders.
- Added README placeholders.
- Did not create `MonoJoey.sln`, `MonoJoey.Server`, or `MonoJoey.Server.Tests` because `dotnet` is not available.
- Did not implement gameplay, networking, persistence, stats, or Unity project files.

## Files/folders created

- `README.md`
- `server-dotnet/`
- `server-dotnet/README.md`
- `shared/`
- `shared/README.md`
- `client-unity/`
- `client-unity/README.md`
- `tools/`
- `tools/README.md`

## Files changed

- `README.md`
- `server-dotnet/README.md`
- `shared/README.md`
- `client-unity/README.md`
- `tools/README.md`
- `docs/SESSION_HANDOVER.md`

## Validation commands run and exact results

- `dotnet --version`
  - Result: failed, exit code 1.
  - Output: `/usr/bin/sh: line 1: dotnet: command not found`
- `dotnet build .\\server-dotnet\\MonoJoey.sln`
  - Result: failed, exit code 1.
  - Output: `/usr/bin/sh: line 1: dotnet: command not found`
- `dotnet test .\\server-dotnet\\MonoJoey.sln`
  - Result: failed, exit code 1.
  - Output: `/usr/bin/sh: line 1: dotnet: command not found`
- `git status --short --branch`
  - Result: succeeded.
  - Output before handover update:

```text
## main...origin/main
 A README.md
 A client-unity/README.md
 A server-dotnet/README.md
 A shared/README.md
 A tools/README.md
?? .pi/
?? monojoey_docs_phase_plan.patch
```

## Known issues

- .NET SDK is unavailable in the current shell (`dotnet: command not found`).
- `server-dotnet/MonoJoey.sln` does not exist yet.
- `server-dotnet/MonoJoey.Server` does not exist yet.
- `server-dotnet/MonoJoey.Server.Tests` does not exist yet.
- Validation build/test could not run because `dotnet` is missing.
- Pre-existing untracked items remain untouched: `.pi/` and `monojoey_docs_phase_plan.patch`.

## Placeholders introduced or preserved

Introduced README-only placeholders for:

- Future authoritative .NET server area in `server-dotnet/`.
- Future shared protocol/contracts area in `shared/`.
- Future Unity client area in `client-unity/`.
- Future tooling area in `tools/`.

Preserved project placeholder rules:

- No Monopoly branding or protected content.
- No gameplay logic.
- No Unity project files or metadata.

## Important decisions preserved

- Unity PC client later.
- C#/.NET authoritative server later.
- WebSockets V1 later.
- Server-owned rules state.
- Client remains presentation-only later.
- Mandatory auctions and Loan Shark mode remain future first-class toggleable systems.

## Next recommended chunk

Continue Phase 1, Chunk 1.1 after the .NET SDK is available in the shell:

- Run `dotnet --version` first.
- Create `server-dotnet/MonoJoey.sln`.
- Create `MonoJoey.Server`.
- Create `MonoJoey.Server.Tests`.
- Add projects to the solution.
- Keep generated/default code minimal.
- Run `dotnet build .\\server-dotnet\\MonoJoey.sln`.
- Run `dotnet test .\\server-dotnet\\MonoJoey.sln`.

Do not advance to Phase 1, Chunk 1.2 until the actual .NET solution/projects exist and validation passes.

## Context docs needed next session

Read only:

- `docs/README.md`
- `docs/AGENT_RULES.md`
- `docs/BUILD_PHASES.md`
- `docs/TECH_ARCHITECTURE.md`
- `docs/SESSION_HANDOVER.md`

Relevant focus files/directories:

- Repo root
- `server-dotnet/`
- `shared/`
- `client-unity/README.md`
- `tools/README.md`

## Do not touch notes for next session

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
- Unity animations
- Unity editor UI
- Unity scenes, prefabs, assets, packages, project settings, or metadata

Do not modify unrelated docs except `docs/SESSION_HANDOVER.md` for the next handover.

## Fresh-session recommendation

Yes — continue in a fresh session once the .NET SDK is available or the shell environment is corrected.

## Context-risk warnings

- Context risk is currently low.
- Drift risk increases if the next session tries to proceed to protocol/domain primitives before the solution/projects are generated.
- The next session should treat this as an incomplete SDK-blocked Chunk 1.1 continuation, not as permission to start gameplay or protocol work.
