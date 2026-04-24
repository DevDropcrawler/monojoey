
# MonoJoey Agent Rules

These rules apply to pi.dev agent, Codex CLI, and any coding assistant working on this repo.

## Core instruction

Build MonoJoey in small, reviewable chunks. Do not drift. Do not overbuild. Do not read the whole repo unless necessary.

## Required session flow

Every session must follow this order:

1. Read `docs/README.md`.
2. Read `docs/AGENT_RULES.md`.
3. Read `docs/BUILD_PHASES.md`.
4. Read `docs/SESSION_HANDOVER.md` if it exists.
5. Read only the domain docs required for the active chunk.
6. Summarize relevant context.
7. Inspect relevant files.
8. Produce a concise implementation plan.
9. Implement only the assigned chunk.
10. Run available tests/build checks.
11. Update docs if needed.
12. Update `docs/SESSION_HANDOVER.md`.
13. Commit only if tests pass and scope is clean.

## Chunk sizing

Target per chunk:

- 1 to 3 hours of coding.
- 3 to 10 files touched.
- One concrete capability.
- Tests or validation included.
- Handover written.

If the chunk becomes larger, stop and split it.

## Context discipline

Do not load excessive files.

Default context for a coding session:

- `docs/README.md`
- `docs/AGENT_RULES.md`
- `docs/BUILD_PHASES.md`
- `docs/SESSION_HANDOVER.md`
- One relevant domain doc.
- Relevant source files only.

## Protected architecture

Do not change without explicit user approval:

- Unity client.
- C#/.NET server.
- WebSocket V1 transport.
- Server-authoritative multiplayer.
- PC-first platform.
- Top-down/angled top-down tabletop presentation.
- Mandatory auction and Loan Shark mechanics as first-class toggleable systems.

## Legal/neutral wording rules

Do not include:

- Monopoly branding.
- Exact Monopoly card wording.
- Monopoly board property names.
- Protected artwork.
- Final copyrighted token model assumptions.

Use placeholders and functional descriptions until user provides original final content.

## Multiplayer trust rules

The client cannot decide:

- Dice results.
- Money balances.
- Property ownership.
- Auction winners.
- Valid bids.
- Loan eligibility.
- Bankruptcy.
- Match winner.

The client may request actions only.

## Animation rules

Client animations must follow server events.

Do not make animation timing determine game truth.

## Testing rules

Server logic chunks must include tests where practical.

Minimum expectations:

- Auction state machine chunks require auction tests.
- Loan Shark chunks require anti-loop and start-of-turn interest tests.
- Card chunks require action resolution tests.
- Stats chunks require aggregation tests.
- Protocol chunks require serialization/message validation tests where practical.

## Commit rules

Commit only when:

- Scope matches assigned chunk.
- Tests/checks pass or known failures are documented honestly.
- `SESSION_HANDOVER.md` is updated.
- Placeholder changes are recorded in `PLACEHOLDER_LEDGER.md`.

Commit message format:

```text
phase-<number>-<chunk-id>: <short summary>
```

Example:

```text
phase-3-auction-02: add auction bid reset state machine
```

## Reasoning level rule

Each agent prompt must specify reasoning level:

- `Reasoning: medium` for normal implementation chunks.
- `Reasoning: high` for auction, loan, bankruptcy, protocol, persistence, and stats correctness.
- `Reasoning: xhigh` only for architecture changes or trust-boundary decisions.

## Stop conditions

Stop and write handover if:

- Tests fail and cannot be fixed within the chunk.
- You need to change architecture.
- You need to change a protected decision.
- The chunk expands beyond intended scope.
- You are unsure whether a change violates server-authoritative rules.

## Required handover contents

`docs/SESSION_HANDOVER.md` must include:

- Last completed chunk.
- Branch.
- Commit.
- What changed.
- Files changed.
- Tests/checks run.
- Known issues.
- Placeholders introduced/preserved.
- Next recommended chunk.
- Context needed next session.
- Do-not-touch notes.
