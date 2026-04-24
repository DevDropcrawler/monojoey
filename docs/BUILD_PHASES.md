
# MonoJoey Build Phases and Coding Chunks

This is the execution map for pi.dev agent + Codex CLI.

Each chunk must be small, testable, and handover-friendly.

## Chunk format

Each chunk includes:

- Goal.
- Reasoning level.
- Required reads.
- Scope.
- Tests/checks.
- Handover requirements.

## Phase 0 — Docs and source-of-truth bootstrap

### 0.1 Docs bootstrap

Reasoning: medium

Goal:

- Add the initial docs in this patch.
- Establish source-of-truth project direction.

Required reads:

- N/A if applying initial patch.

Scope:

- Create `docs/` docs listed in `README.md`.

Checks:

- Confirm files exist.

Handover:

- Update `SESSION_HANDOVER.md` with applied patch status.

## Phase 1 — Repo and server skeleton

### 1.1 Repo skeleton and .NET solution

Reasoning: medium

Goal:

- Create repo folders.
- Create .NET solution and projects.

Required reads:

- `docs/README.md`
- `docs/AGENT_RULES.md`
- `docs/TECH_ARCHITECTURE.md`
- `docs/SESSION_HANDOVER.md`

Scope:

- Create `server-dotnet/MonoJoey.sln`.
- Create `MonoJoey.Server`.
- Create `MonoJoey.Server.Tests`.
- Create `shared/MonoJoey.Shared` if practical.
- Add root `.gitignore`.
- Add basic `README.md` at repo root if missing.

Checks:

- `dotnet build`
- `dotnet test`

Handover:

- Record exact commands run.

### 1.2 Shared protocol/domain primitives

Reasoning: high

Goal:

- Add shared IDs/enums/DTO foundations without gameplay logic.

Required reads:

- `docs/TECH_ARCHITECTURE.md`
- `docs/MULTIPLAYER_PROTOCOL.md`
- `docs/DATA_SCHEMAS.md`

Scope:

- Add ID value objects or simple typed records.
- Add core enums: tile type, game phase, card action type, money reason.
- Add draft request/event type enums.
- Add basic serialization-safe DTOs where useful.

Checks:

- `dotnet test`

Handover:

- Note any schema deviations from docs.

### 1.3 Server test harness baseline

Reasoning: medium

Goal:

- Create a clean server unit-test structure before implementing rules.

Required reads:

- `docs/TECH_ARCHITECTURE.md`
- `docs/AGENT_RULES.md`

Scope:

- Add test project dependencies.
- Add first trivial test.
- Add test naming conventions.

Checks:

- `dotnet test`

Handover:

- Document test command.

## Phase 2 — Core server rules engine

### 2.1 Match/player/board state models

Reasoning: high

Goal:

- Add server-side state models only.

Required reads:

- `docs/RULES_ENGINE.md`
- `docs/DATA_SCHEMAS.md`

Scope:

- MatchState.
- PlayerState.
- BoardConfig.
- TileConfig.
- RulesetConfig skeleton.
- In-memory default board builder with placeholders.

Checks:

- Unit tests for default board validity.
- `dotnet test`

### 2.2 Turn lifecycle skeleton

Reasoning: high

Goal:

- Add turn manager and phase validation skeleton.

Required reads:

- `docs/RULES_ENGINE.md`

Scope:

- Start match.
- Select first player.
- Start turn.
- Await roll.
- End turn.
- Skip bankrupt players.

Checks:

- Tests for turn order and phase rejection.

### 2.3 Dice service and movement

Reasoning: high

Goal:

- Server-owned dice and movement resolution.

Required reads:

- `docs/RULES_ENGINE.md`
- `docs/MULTIPLAYER_PROTOCOL.md`

Scope:

- Dice service with injectable deterministic RNG for tests.
- Move around board.
- Pass-start reward handling.
- Emit movement events internally.

Checks:

- Tests for dice range.
- Tests for wrap-around/pass-start.
- Tests for no client-supplied dice.

### 2.4 Property ownership and rent baseline

Reasoning: high

Goal:

- Add property ownership/rent basics without auctions yet.

Required reads:

- `docs/RULES_ENGINE.md`

Scope:

- Owned/unowned property state.
- Rent calculation from placeholder rent table.
- Pay rent.
- Basic insufficient funds path placeholder.

Checks:

- Tests for rent paid owner.
- Tests for unowned property detection.

### 2.5 Bankruptcy baseline

Reasoning: high

Goal:

- Add elimination/bankruptcy state.

Required reads:

- `docs/RULES_ENGINE.md`

Scope:

- Mark player bankrupt.
- Remove from active turn rotation.
- Determine final active player winner.

Checks:

- Tests for bankrupt player skipped.
- Tests for match completion when one player remains.

## Phase 3 — Mandatory auction system

### 3.1 Auction config and state model

Reasoning: high

Goal:

- Add auction config/state without timer engine.

Required reads:

- `docs/RULES_ENGINE.md`
- `docs/DATA_SCHEMAS.md`

Scope:

- AuctionConfig.
- AuctionState.
- AuctionStatus enum.
- Default values: 9s initial, 3s reset, configurable increment.

Checks:

- Tests for default config.

### 3.2 Start auction on unowned property

Reasoning: high

Goal:

- Mandatory auction starts when landing on unowned auctionable property.

Required reads:

- `docs/RULES_ENGINE.md`

Scope:

- Integrate landing resolution with auction start.
- Direct purchase disabled when mandatory auctions enabled.

Checks:

- Test landing starts auction.
- Test no direct purchase path in mandatory mode.

### 3.3 Bid validation and bid acceptance

Reasoning: high

Goal:

- Validate bid amounts and active bidders.

Required reads:

- `docs/RULES_ENGINE.md`
- `docs/MULTIPLAYER_PROTOCOL.md`

Scope:

- First bid >= starting bid.
- Later bid >= current + minimum increment.
- Reject bankrupt/inactive bidders.
- Highest bidder/amount updates.

Checks:

- Bid too low.
- Valid first bid.
- Valid reset bid.
- Bankrupt bidder rejected.

### 3.4 Auction timer transition logic

Reasoning: high

Goal:

- Implement 9-second pre-bid and 3-second bid-reset countdown behavior.

Required reads:

- `docs/RULES_ENGINE.md`

Scope:

- Initial no-bid timer.
- First bid switches mode.
- Every valid bid resets timer.
- Expiry no bids = no sale.
- Expiry with highest bid = winner.

Checks:

- No bids after 9s = no sale.
- First bid starts 3s timer.
- New bid resets 3s timer.
- Expiry sells to highest bidder.

### 3.5 Auction property transfer and payment

Reasoning: high

Goal:

- Complete auction win resolution.

Required reads:

- `docs/RULES_ENGINE.md`

Scope:

- Winner pays bank.
- Property ownership set.
- Stats event emitted.
- Turn resumes/ends as designed.

Checks:

- Money deducted.
- Property owner set.
- Highest bidder wins.

## Phase 4 — Loan Shark system

### 4.1 Loan config/account model

Reasoning: high

Goal:

- Add loan config and per-player account.

Required reads:

- `docs/RULES_ENGINE.md`
- `docs/DATA_SCHEMAS.md`

Scope:

- LoanSharkConfig.
- LoanAccount.
- Defaults from docs.

Checks:

- Default config tests.

### 4.2 Borrow reason validation

Reasoning: high

Goal:

- Prevent infinite debt loop by blocking loan payments as borrow reasons.

Required reads:

- `docs/RULES_ENGINE.md`

Scope:

- Allow AuctionBid/RentPayment/TaxPayment/CardPenalty/Fine.
- Block LoanInterest/LoanPrincipalRepayment/ExistingLoanDebt.

Checks:

- Each allowed reason allowed.
- Each blocked reason blocked.

### 4.3 Escalating interest calculation

Reasoning: high

Goal:

- Calculate increasingly harsh interest as borrowing increases.

Required reads:

- `docs/RULES_ENGINE.md`

Scope:

- Loan count increase.
- Principal outstanding.
- Current interest rate.
- Interest due next turn.

Checks:

- First loan rate.
- Later loans higher.
- Minimum interest payment.

### 4.4 Start-of-turn interest charge

Reasoning: high

Goal:

- Charge interest before roll.

Required reads:

- `docs/RULES_ENGINE.md`

Scope:

- Turn start checks interest due.
- Deducts before `AwaitingRoll`.
- Emits event.

Checks:

- Interest charged before roll phase.
- No roll allowed before interest resolution.

### 4.5 Loan-interest bankruptcy

Reasoning: high

Goal:

- Eliminate player if start-of-turn interest bankrupts them.

Required reads:

- `docs/RULES_ENGINE.md`

Scope:

- If player cannot pay interest and cannot legally recover, bankrupt.
- Advance turn.
- Match may complete.

Checks:

- Interest bankruptcy eliminates before roll.
- Borrowing to pay interest is impossible.

## Phase 5 — Placeholder card system

### 5.1 Card/deck models

Reasoning: medium

Goal:

- Add placeholder card config/deck state.

Required reads:

- `docs/RULES_ENGINE.md`
- `docs/DATA_SCHEMAS.md`
- `docs/PLACEHOLDER_LEDGER.md`

Scope:

- CardConfig.
- DeckState.
- Draw/discard basics.

Checks:

- Draw card cycles deck.

### 5.2 Placeholder decks

Reasoning: medium

Goal:

- Add 16 Chance-style and 16 Table-style placeholder cards.

Required reads:

- `docs/RULES_ENGINE.md`
- `docs/PLACEHOLDER_LEDGER.md`

Scope:

- Safe functional names/descriptions only.
- No exact Monopoly wording.

Checks:

- 16 cards per deck.
- All cards enabled by default.

### 5.3 Card action resolver

Reasoning: high

Goal:

- Resolve core card actions.

Required reads:

- `docs/RULES_ENGINE.md`

Scope:

- MoveToTile.
- MoveRelative.
- MoveToNearestType.
- ReceiveFromBank.
- PayBank.
- PayEachOpponent.
- CollectFromEachOpponent.
- PayPerUpgrade.
- HoldableCancelStatus.

Checks:

- One test per action type minimum.

## Phase 6 — Lobby and WebSocket server

### 6.1 WebSocket server foundation

Reasoning: high

Goal:

- Add basic WebSocket endpoint and message envelope.

Required reads:

- `docs/MULTIPLAYER_PROTOCOL.md`
- `docs/TECH_ARCHITECTURE.md`

Scope:

- Accept connections.
- Parse envelope.
- Return error for unknown message.

Checks:

- Build/test.
- Minimal local smoke if practical.

### 6.2 Create/join lobby

Reasoning: high

Goal:

- Create private lobbies and join by code.

Required reads:

- `docs/MULTIPLAYER_PROTOCOL.md`

Scope:

- Create lobby.
- Join lobby.
- Broadcast lobby membership.

Checks:

- Unit/service tests.

### 6.3 Username/token/color selection

Reasoning: high

Goal:

- Add player selection validation.

Required reads:

- `docs/GAME_DESIGN.md`
- `docs/DATA_SCHEMAS.md`

Scope:

- Username.
- Placeholder token ID.
- Color ID.
- Unique token/color validation.

Checks:

- Duplicate token rejected.
- Duplicate color rejected.
- Valid profile accepted.

### 6.4 Ready/start match

Reasoning: high

Goal:

- Ready state and match start.

Required reads:

- `docs/MULTIPLAYER_PROTOCOL.md`

Scope:

- Set ready.
- Start only with enough ready players.
- Create MatchState from lobby.

Checks:

- Start rejected if not enough players.
- Start accepted with valid ready lobby.

### 6.5 Gameplay request routing

Reasoning: high

Goal:

- Route RollDice/PlaceBid/TakeLoan to rules engine.

Required reads:

- `docs/MULTIPLAYER_PROTOCOL.md`
- `docs/RULES_ENGINE.md`

Scope:

- Request handlers.
- Server event broadcasts.
- Phase validation.

Checks:

- Build/test.

## Phase 7 — Unity client skeleton

### 7.1 Unity project bootstrap

Reasoning: medium

Goal:

- Create Unity project structure.

Required reads:

- `docs/TECH_ARCHITECTURE.md`
- `docs/ANIMATION_STYLE.md`

Scope:

- `client-unity/` project.
- Basic scenes: MainMenu, Lobby, Board.
- Placeholder folder structure.

Checks:

- Unity project opens/builds if environment supports it.

### 7.2 Unity WebSocket client

Reasoning: high

Goal:

- Connect Unity client to local server.

Required reads:

- `docs/MULTIPLAYER_PROTOCOL.md`

Scope:

- Connection manager.
- Send envelope.
- Receive event.
- Basic log panel.

Checks:

- Manual/local smoke where possible.

### 7.3 Lobby UI

Reasoning: medium

Goal:

- Basic create/join lobby UI.

Required reads:

- `docs/MULTIPLAYER_PROTOCOL.md`

Scope:

- Create lobby button.
- Join code input.
- Player list.

Checks:

- Manual smoke.

### 7.4 Token/username selection UI

Reasoning: medium

Goal:

- Player setup screen.

Required reads:

- `docs/GAME_DESIGN.md`
- `docs/PLACEHOLDER_LEDGER.md`

Scope:

- Username field.
- Placeholder token buttons.
- Color buttons.
- Ready button.

Checks:

- Server rejects duplicates.

### 7.5 Simple board scene

Reasoning: medium

Goal:

- Render a simple top-down placeholder board.

Required reads:

- `docs/ANIMATION_STYLE.md`

Scope:

- Board tiles.
- Placeholder tokens.
- Camera top-down/angled.
- Basic UI panels.

Checks:

- Manual smoke.

## Phase 8 — Animation queue and premium feel foundation

### 8.1 Client animation queue

Reasoning: high

Goal:

- Add event-to-animation queue foundation.

Required reads:

- `docs/ANIMATION_STYLE.md`
- `docs/MULTIPLAYER_PROTOCOL.md`

Scope:

- Queue server events.
- Play sequential visual actions.
- Skip/fast mode placeholder.

Checks:

- Manual smoke/log tests.

### 8.2 Dice and token movement animation

Reasoning: medium

Goal:

- Animate dice result and token movement from server events.

Required reads:

- `docs/ANIMATION_STYLE.md`

Scope:

- Dice visual settles to server result.
- Token glides tile-to-tile.

Checks:

- Manual smoke.

### 8.3 Auction UI animation

Reasoning: medium

Goal:

- Animate auction panel and timers.

Required reads:

- `docs/ANIMATION_STYLE.md`
- `docs/RULES_ENGINE.md`

Scope:

- 9s initial timer display.
- 3s bid reset display.
- Highest bidder display.

Checks:

- Manual smoke with server events.

### 8.4 Card/money/property feedback

Reasoning: medium

Goal:

- Add placeholder premium feedback for cards, money, ownership.

Required reads:

- `docs/ANIMATION_STYLE.md`

Scope:

- Card flip/slide.
- Money transfer placeholder.
- Property ownership marker.

Checks:

- Manual smoke.

## Phase 9 — Persistent stats and leaderboards

### 9.1 Match result model and stat events

Reasoning: high

Goal:

- Produce match result summary from completed matches.

Required reads:

- `docs/GAME_DESIGN.md`
- `docs/DATA_SCHEMAS.md`

Scope:

- MatchResult.
- Per-player result.
- Stat events captured from rules engine.

Checks:

- Tests for summary fields.

### 9.2 Lifetime stats aggregation

Reasoning: high

Goal:

- Aggregate cross-game player stats.

Required reads:

- `docs/DATA_SCHEMAS.md`

Scope:

- Games played/won/lost.
- Money/rent/auction/loan/card/dice stats.

Checks:

- Aggregation tests.

### 9.3 Leaderboard queries

Reasoning: high

Goal:

- Add leaderboard service categories.

Required reads:

- `docs/GAME_DESIGN.md`

Scope:

- Most wins.
- Best win rate.
- Most auctions won.
- Most interest paid.
- Most bankruptcies caused.

Checks:

- Sorting/filter tests.

### 9.4 Persistence abstraction

Reasoning: high

Goal:

- Prepare for database without locking in implementation too early.

Required reads:

- `docs/TECH_ARCHITECTURE.md`

Scope:

- Repository interfaces.
- In-memory implementation.

Checks:

- Existing tests pass.

### 9.5 PostgreSQL implementation

Reasoning: high

Goal:

- Add real persistence when core stats are stable.

Required reads:

- `docs/TECH_ARCHITECTURE.md`
- `docs/DATA_SCHEMAS.md`

Scope:

- DB schema/migrations.
- Match result persistence.
- Lifetime stats persistence.

Checks:

- Migration test/smoke if environment supports.

## Phase 10 — Customization editor later

### 10.1 Ruleset schema validation

Reasoning: high

Goal:

- Validate custom rulesets server-side.

Required reads:

- `docs/DATA_SCHEMAS.md`
- `docs/RULES_ENGINE.md`

Scope:

- Ruleset validator.
- Safe bounds for timers, bid increments, interest rates.

Checks:

- Invalid config rejected.

### 10.2 Board config validation

Reasoning: high

Goal:

- Validate custom boards server-side.

Required reads:

- `docs/DATA_SCHEMAS.md`

Scope:

- Tile order.
- Required Start tile.
- Valid targets.
- Valid property groups.

Checks:

- Invalid board rejected.

### 10.3 Unity rules/card/board editor screens

Reasoning: medium

Goal:

- Build editor UI after core game works.

Required reads:

- `docs/GAME_DESIGN.md`
- `docs/DATA_SCHEMAS.md`

Scope:

- Rules editor.
- Card editor.
- Board/tile editor.

Checks:

- Manual save/load smoke.

## First agent prompt

Use this first after applying the patch:

```text
Reasoning: medium

You are working on MonoJoey.

Read:
- docs/README.md
- docs/AGENT_RULES.md
- docs/BUILD_PHASES.md
- docs/TECH_ARCHITECTURE.md
- docs/SESSION_HANDOVER.md

Task: Execute Phase 1, Chunk 1.1 only: repo skeleton and .NET solution.

Rules:
- Do not implement gameplay yet.
- Do not create Unity gameplay yet.
- Do not change protected architecture decisions.
- Keep scope small.
- Create server-dotnet .NET solution/projects and shared project if practical.
- Add root .gitignore if missing.
- Run dotnet build and dotnet test if available.
- Update docs/SESSION_HANDOVER.md with what changed, checks run, known issues, and next recommended chunk.
- Commit only if checks pass and scope is clean.
```
