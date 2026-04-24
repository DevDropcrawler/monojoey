
# MonoJoey Documentation Index

This folder is the source of truth for MonoJoey.

MonoJoey is a PC-first, online multiplayer, premium top-down 3D tabletop property-trading game with fully configurable rules, cards, auctions, loans, player tokens, stats, leaderboards, and later board/theme editing.

## Non-negotiable project pillars

1. Server-authoritative online multiplayer from day one.
2. Unity PC client with premium top-down tabletop presentation.
3. Buttery-smooth animations as a core gameplay pillar.
4. Clean C#/.NET backend rules engine.
5. WebSocket event protocol between client and server.
6. Data-driven boards, cards, rules, tiles, and player tokens.
7. Mandatory auction mode must be first-class and toggleable.
8. Loan Shark mode must be first-class, toggleable, escalating, and anti-loop protected.
9. Placeholder card/token content only during core-game phase.
10. Persistent cross-game stats, total game counter, and detailed leaderboards.
11. No Monopoly branding, exact card wording, protected names, or final copyrighted token/model assumptions.

## Required docs

- `GAME_DESIGN.md` — product vision, gameplay pillars, default V1 scope.
- `TECH_ARCHITECTURE.md` — engine/backend/networking/folder decisions.
- `RULES_ENGINE.md` — server-owned game state and modular rule systems.
- `MULTIPLAYER_PROTOCOL.md` — WebSocket message/event conventions.
- `ANIMATION_STYLE.md` — premium top-down tabletop animation rules.
- `DATA_SCHEMAS.md` — board, tile, card, player, ruleset, stats schemas.
- `PLACEHOLDER_LEDGER.md` — all temporary placeholders and replacement notes.
- `BUILD_PHASES.md` — phases and Codex/pi.dev coding chunks.
- `AGENT_RULES.md` — strict execution rules for pi.dev/Codex sessions.
- `SESSION_HANDOVER.md` — rolling handover template for fresh sessions.

## Default agent workflow

Every coding chunk must follow:

1. Read the required docs for the chunk only.
2. Summarize relevant context.
3. Inspect only relevant files.
4. Produce a small plan.
5. Implement only the assigned chunk.
6. Run available tests/build checks.
7. Update docs if the implementation changes contracts, placeholders, or next steps.
8. Write `docs/SESSION_HANDOVER.md`.
9. Commit only if tests pass and scope is clean.

## Fresh session rule

Fresh Codex sessions must not read the entire repo by default. They should read:

- `docs/README.md`
- `docs/AGENT_RULES.md`
- `docs/BUILD_PHASES.md`
- `docs/SESSION_HANDOVER.md`
- only the specific domain doc for the active chunk

## Protected decisions

These decisions are frozen unless the user explicitly changes them:

- Engine: Unity.
- Client language: C#.
- Server language: C#/.NET.
- Platform: PC first.
- Network model: server-authoritative, not peer-to-peer.
- Transport: WebSockets for V1.
- Database: PostgreSQL later, after server core is stable.
- View: top-down / angled top-down premium tabletop.
- Core loop first, editor later.

## Forbidden drift

Do not:

- Convert to Unreal/Godot without explicit user approval.
- Make multiplayer peer-to-peer.
- Let the Unity client decide authoritative money, dice, auctions, rent, or bankruptcy.
- Spend time finalizing custom card text before core game works.
- Use exact Monopoly card wording, branding, character names, board names, or protected artwork.
- Build Steam/Workshop integration before the playable online core exists.
- Add cosmetic systems before the core server-client loop is working.
