
# MonoJoey Session Handover

This file must be updated at the end of every coding chunk.

## Current status

- Phase: 0
- Chunk: docs-bootstrap
- Branch: TBD
- Commit: TBD
- Date/time: TBD

## Last completed chunk

- None yet. Initial docs patch prepared.

## What changed

- Initial planning docs created.
- Build phases defined.
- Agent rules defined.
- Core architecture captured.

## Files changed

- `docs/README.md`
- `docs/GAME_DESIGN.md`
- `docs/TECH_ARCHITECTURE.md`
- `docs/RULES_ENGINE.md`
- `docs/MULTIPLAYER_PROTOCOL.md`
- `docs/ANIMATION_STYLE.md`
- `docs/DATA_SCHEMAS.md`
- `docs/PLACEHOLDER_LEDGER.md`
- `docs/AGENT_RULES.md`
- `docs/BUILD_PHASES.md`
- `docs/SESSION_HANDOVER.md`

## Tests/checks run

- Not applicable for docs patch.

## Known issues

- No repo code exists yet.
- Unity project not bootstrapped.
- .NET server not bootstrapped.

## Placeholders introduced or preserved

- Placeholder card names and safe functional card descriptions.
- Placeholder tabletop token IDs.
- Placeholder board/property/currency names.

## Important decisions made

- Unity PC client.
- C#/.NET authoritative server.
- WebSockets V1.
- Server-owned rules state.
- Premium top-down tabletop visual direction.
- Mandatory auctions and Loan Shark mode are first-class toggleable systems.

## Next recommended chunk

Start with Phase 1, Chunk 1.1:

- Create repo skeleton and .NET solution.
- Add docs folder using this patch if not already applied.
- Add basic `.gitignore` and solution structure.

## Context needed next session

Read:

- `docs/README.md`
- `docs/AGENT_RULES.md`
- `docs/BUILD_PHASES.md`
- `docs/TECH_ARCHITECTURE.md`
- `docs/SESSION_HANDOVER.md`

Focus files:

- Repo root.
- `server-dotnet/`
- `shared/`
- `docs/`

Do not touch:

- Unity gameplay implementation before server skeleton exists.
