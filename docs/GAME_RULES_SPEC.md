# MonoJoey Game Rules Customization Spec

## Purpose

This is the canonical Phase 5.22 contract for rules customization, presets, custom cards, deck editing, live-edit safety, and future protocol projection.

`docs/RULES_ENGINE.md` remains the server execution behavior contract: it describes how authoritative gameplay systems validate and resolve turns, auctions, loans, cards, bankruptcy, and completion. This document describes the editable configuration layer that the server will validate, snapshot, and apply to those systems.

Phase 5.22 is documentation only. It does not implement backend behavior, Unity UI, rule voting, card execution changes, deck editing, Slimer, Earthquake, or new runtime systems.

## Core Principles

- Rules are validated configuration that shape server behavior before a match starts and, where safe, during a match.
- Cards are data-driven definitions containing user-facing display fields plus structured executable fields.
- `displayText` is display-only. Server logic must use only `actionType` and validated `parameters`.
- The server remains authoritative. Clients may request changes, but the server validates, stores, snapshots, and executes the active configuration.
- Runtime state and configuration definitions are separate. Deck definitions, card definitions, draw piles, discard piles, held cards, and resolved rule values must not be conflated.
- Future mechanics are extension points only in this phase. Slimer belongs to a future `StatusEffectSystem`; Earthquake belongs to a future `PropertyStateSystem`.
- Do not use protected board names, card text, branding, artwork, or final copyrighted token assumptions.

## Editor Categories

Rules should be grouped in the control panel with these categories:

- Overview
- Money & Economy
- Property & Rent
- Auctions
- Jail / Lockup
- Dice & Movement
- Cards
- Loans
- Win Conditions
- Future Mechanics / Experimental

## Rule Schema

Every editable rule must use this metadata shape:

```json
{
  "ruleId": "auction.minimumBidIncrement",
  "category": "Auctions",
  "type": "integer",
  "default": 1,
  "preGameEditable": true,
  "inGameEditable": "conditional",
  "liveEditSafety": "conditional",
  "validation": {
    "min": 1,
    "max": 1000
  },
  "dependencies": [
    "auction.mandatoryAuctionsEnabled"
  ],
  "notes": "Only affects auctions that have not started yet."
}
```

Fields:

- `ruleId`: Stable dotted identifier used by validation, storage, snapshots, and protocol payloads.
- `category`: One of the editor categories above.
- `type`: Scalar or structured editor type such as `boolean`, `integer`, `decimal`, `string`, `enum`, `list`, or `object`.
- `default`: Default value after preset resolution.
- `preGameEditable`: Whether a lobby can change the rule before match start.
- `inGameEditable`: `true`, `false`, or `conditional`.
- `liveEditSafety`: `safe`, `locked`, or `conditional`.
- `validation`: Constraints enforced by the server before storing the value.
- `dependencies`: Other rules, board capabilities, or deck/card definitions required for the value to be valid.
- `notes`: Human-readable implementation and fairness notes. These are not executable.

## Baseline Rule Set

These are planning identifiers for the editable surface. They do not require Phase 5.22 implementation.

| ruleId | Category | Type | Default | Pre-game | In-game | Safety | Validation | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `preset.id` | Overview | string | `monojoey_default` | true | false | locked | Known preset ID or saved preset ID | Identifies the source preset. |
| `preset.displayName` | Overview | string | `MonoJoey default` | true | true | safe | 1 to 64 characters | Display-only name. |
| `preset.isCustom` | Overview | boolean | false | server | server | locked | Server-derived | True once resolved values differ from a named preset. |
| `economy.startingMoney` | Money & Economy | integer | 1500 | true | false | locked | Min 0 | Changing after start would require retroactive balance migration. |
| `economy.passStartReward` | Money & Economy | integer | 200 | true | conditional | conditional | Min 0 | Only safe before any player movement can pass the start tile under the new value. |
| `property.baseRentEnabled` | Property & Rent | boolean | true | true | conditional | conditional | boolean | Only safe before unresolved rent under the current turn. |
| `property.upgradesEnabled` | Property & Rent | boolean | false | true | false | locked | boolean | Future rule; upgrades are not implemented in Phase 5.22. |
| `auction.mandatoryAuctionsEnabled` | Auctions | boolean | true | true | conditional | conditional | boolean | Only safe while no auction is active and no current tile is awaiting auction execution. |
| `auction.initialTimerSeconds` | Auctions | integer | 9 | true | conditional | conditional | Min 1 | Current implementation default is 9. Applies only before an auction starts. |
| `auction.bidResetTimerSeconds` | Auctions | integer | 3 | true | conditional | conditional | Min 1 | Current implementation default is 3. Applies only before an auction starts. |
| `auction.minimumBidIncrement` | Auctions | integer | 1 | true | conditional | conditional | Min 1 | Current implementation default is 1. Applies only before an auction starts. |
| `auction.startingBid` | Auctions | integer | 0 | true | conditional | conditional | Min 0 | Current implementation default is 0. Applies only before an auction starts. |
| `lockup.enabled` | Jail / Lockup | boolean | true | true | false | locked | boolean | Lockup state affects turn eligibility and held escape cards. |
| `lockup.escapeCardsEnabled` | Jail / Lockup | boolean | true | true | conditional | conditional | boolean | Only safe before relevant held-card or deck references exist. |
| `dice.diceCount` | Dice & Movement | integer | 2 | true | false | locked | Min 1 | Dice metadata and doubles behavior depend on this. |
| `dice.sidesPerDie` | Dice & Movement | integer | 6 | true | false | locked | Min 2 | Movement and probability expectations depend on this. |
| `movement.resolveLandingAfterCardMove` | Dice & Movement | boolean | card-defined | true | false | locked | boolean or card-defined | Runtime effect must come from validated card parameters. |
| `cards.decksEnabled` | Cards | list | `["chance", "table"]` | true | conditional | conditional | Known deck IDs | A deck can only be toggled before it has been drawn from. |
| `cards.customCardsEnabled` | Cards | boolean | true | true | false | locked | boolean | Enables pre-game custom card definitions. |
| `cards.deckEditingEnabled` | Cards | boolean | true | true | false | locked | boolean | Phase 5.22 deck editing is pre-game only. |
| `loans.loanSharkEnabled` | Loans | boolean | true | true | conditional | conditional | boolean | Only safe before any player has borrowed. |
| `loans.baseInterestRate` | Loans | decimal | 0.25 | true | conditional | conditional | 0.0 to 1.0 | Only safe before any player has borrowed. |
| `loans.interestRateIncreasePerLoan` | Loans | decimal | 0.10 | true | conditional | conditional | 0.0 to 1.0 | Only safe before any player has borrowed. |
| `loans.interestRateIncreasePerDebtTier` | Loans | decimal | 0.05 | true | conditional | conditional | 0.0 to 1.0 | Only safe before any player has borrowed. |
| `loans.minimumInterestPayment` | Loans | integer | 25 | true | conditional | conditional | Min 0 | Only safe before any player has borrowed. |
| `loans.canBorrowForLoanPayments` | Loans | boolean | false | true | conditional | conditional | boolean | Anti-loop protection remains server-enforced. |
| `win.conditionType` | Win Conditions | enum | `lastPlayerStanding` | true | false | locked | Known win condition | Runtime completion logic depends on this. |
| `future.slimer.enabled` | Future Mechanics / Experimental | boolean | false | true | false | locked | boolean | Schema/config-only toggle for future status effects. |
| `future.earthquake.enabled` | Future Mechanics / Experimental | boolean | false | true | false | locked | boolean | Schema/config-only toggle for future property damage. |

## Presets

Presets are named sets of resolved rule values. They must use neutral names and must not include protected card text, board names, branding, artwork, or final copyrighted token assumptions.

Required preset types:

- Classic-style, with machine ID `classic_style`: A familiar property-board-game style configuration using neutral wording and generic placeholders.
- MonoJoey default, with machine ID `monojoey_default`: The default MonoJoey configuration. Current known auction defaults are `auction.initialTimerSeconds = 9`, `auction.bidResetTimerSeconds = 3`, `auction.minimumBidIncrement = 1`, and `auction.startingBid = 0`.
- Custom, with machine ID `custom`: Server-derived marker used when the active resolved values no longer exactly match a named preset.
- Future saved presets: User-saved, server-validated preset records with stable IDs and version metadata.

Preset records should include:

```json
{
  "presetId": "monojoey_default",
  "displayName": "MonoJoey default",
  "version": 1,
  "isBuiltIn": true,
  "rules": {
    "auction.initialTimerSeconds": 9,
    "auction.bidResetTimerSeconds": 3,
    "auction.minimumBidIncrement": 1,
    "auction.startingBid": 0
  }
}
```

## Custom Card Records

Card definitions combine display data with structured action data. Display fields are never parsed for gameplay logic.

```json
{
  "cardId": "card_custom_001",
  "deckId": "chance",
  "displayTitle": "Custom movement card",
  "displayText": "Move to a selected tile.",
  "artId": "card_art_placeholder_001",
  "actionType": "MoveToTile",
  "parameters": {
    "targetTileId": "start",
    "collectPassStart": true,
    "resolveLandingTile": false
  },
  "enabled": true
}
```

Fields:

- `cardId`: Stable identifier unique across active card definitions.
- `deckId`: Deck that owns the card.
- `displayTitle`: Player-facing title only.
- `displayText`: Player-facing body text only. It must not drive execution.
- `artId`: Optional art reference. Placeholder art is allowed.
- `actionType`: Server-validated executable action.
- `parameters`: Structured parameters for the selected action type.
- `enabled`: Whether the card is included when the server freezes active deck definitions at game start.

## Card Action Parameter Contracts

Supported contracts:

- `MoveToTile`: `targetTileId`, `collectPassStart`, `resolveLandingTile`.
- `MoveRelative`: `spaces`, `direction`, `collectPassStart`, `resolveLandingTile`.
- `MoveToNearestType`: `tileType`, `multiplier`, `collectPassStart`, `resolveLandingTile`.
- `ReceiveFromBank`: `amount`.
- `PayBank`: `amount`, optional `bankruptcyEligible`.
- `PayEachOpponent`: `amountPerOpponent`, optional `bankruptcyEligible`.
- `CollectFromEachOpponent`: `amountPerOpponent`.
- `PayPerUpgradeOwned`: `amountPerUpgrade`, `upgradeTypes`, optional `bankruptcyEligible`.
- `GoToLockup`: `targetTileId`, `endsTurn`.
- `GiveLockupEscapeCard`: `escapeCardId`, `returnToDeckOnUse`.

Future contracts:

- `ApplyStatusEffect`: `statusEffectId`, `durationTurns`, `target`.
- `TriggerPropertyDamage`: `damageType`, `targetScope`, `repairCost`.

Validation requirements:

- Money values must be non-negative integer amounts unless a future rule explicitly allows signed values.
- Tile IDs must exist on the active board.
- Deck IDs must exist in the active deck set.
- Card IDs must be unique and stable for the match.
- `direction` must be a known movement direction, such as `forward` or `backward`.
- `tileType` must be a known active board tile type.
- Future action types may be stored only when disabled or hidden behind explicit experimental toggles until their systems exist.

## Deck Records

Deck definitions describe the cards available to draw. Runtime pile state is stored separately.

```json
{
  "deckId": "chance",
  "displayName": "Chance-style deck",
  "enabled": true,
  "cards": ["card_custom_001", "card_custom_002"],
  "shuffleMode": "serverSeeded",
  "discardMode": "discardAfterUse",
  "reshuffleWhenEmpty": false
}
```

Fields:

- `deckId`: Stable deck identifier.
- `displayName`: Player-facing deck name.
- `enabled`: Whether the deck can be used by matching board tiles and rules.
- `cards[]`: Ordered card IDs included in this deck definition.
- `shuffleMode`: Deck ordering behavior, such as `fixedOrder` or `serverSeeded`.
- `discardMode`: What happens to a card after execution, such as `discardAfterUse`, `returnToBottom`, or `heldUntilUsed`.
- `reshuffleWhenEmpty`: Whether discard piles can become new draw piles when exhausted.

User capabilities:

- Add cards before game start.
- Remove cards before game start.
- Reorder cards before game start.
- Enable or disable decks before game start.

## Deck Determinism

At game start, the server freezes active deck definitions and initial card order.

The following must be reproducible from authoritative server state:

- Active deck definitions.
- Initial card order.
- Draw pile card IDs.
- Discard pile card IDs.
- Held card IDs per player.
- Reshuffle behavior.
- Any server seed or resolved shuffled order used by the match.

Mid-game deck edits are locked because they would invalidate:

- Deterministic snapshots.
- Reconnect recovery.
- Draw history.
- Draw/discard integrity.
- Held-card references.
- Client projection from authoritative snapshots.

In Phase 5.22, deck editing is pre-game only. In-game deck edits require a future voting and migration system.

## Live-Edit Safety Matrix

| Edit | Safety | Reason |
| --- | --- | --- |
| Display-only preset name | Safe | Does not affect game-state consistency or server execution. |
| Lobby-facing rule description | Safe | Display-only text can update without changing deterministic state. |
| Non-executable card `displayText` | Safe | Server execution uses `actionType` and validated `parameters`, not display text. |
| Disabled future UI labels | Safe | Disabled labels are not part of authoritative match state. |
| Starting money after game start | Locked | Changing it would require balance migration and would break game-state consistency. |
| Deck card list after game start | Locked | Changes would break deck draw/discard integrity and deterministic snapshots. |
| Deck card order after game start | Locked | Changes would rewrite deterministic draw history. |
| Card `actionType` after game start | Locked | Existing snapshots, reconnect projection, and held-card references may already depend on the old action. |
| Card parameters after game start | Locked | Execution semantics must remain stable for active and held cards. |
| Win condition type after game start | Locked | Completion authority and current elimination state depend on it. |
| Board topology after game start | Locked | Player positions, movement paths, tile IDs, and reconnect projection depend on a stable board. |
| Pass-start tile behavior after players have moved | Locked | Prior movement and rewards must remain deterministic and fair. |
| Auction timer values before an auction starts | Conditional | Safe only while no active auction authority or countdown deadline exists. |
| Loan interest settings before any player has borrowed | Conditional | Safe only before loan state exists for any player. |
| Tax amount before a player is resolving that tile | Conditional | Safe only before current-turn fairness can be affected. |
| Enabled card deck before that deck has been drawn from | Conditional | Safe only before draw piles, discard piles, and held-card references exist for that deck. |

Conditional edits require the server to check current authoritative state before accepting the change. A client-side disabled control is only advisory.

## Snapshot And Reconnect Projection

Snapshot data is authoritative. Reconnecting clients must hydrate from snapshots rather than local preset assumptions.

Snapshots must include active rules as a versioned rules object:

```json
{
  "rules": {
    "version": 1,
    "presetId": "monojoey_default",
    "presetName": "MonoJoey default",
    "isCustom": false,
    "values": {
      "auction.initialTimerSeconds": 9,
      "auction.bidResetTimerSeconds": 3,
      "auction.minimumBidIncrement": 1,
      "auction.startingBid": 0
    }
  }
}
```

Snapshots must include active deck definitions:

```json
{
  "deckDefinitions": [
    {
      "deckId": "chance",
      "displayName": "Chance-style deck",
      "enabled": true,
      "shuffleMode": "fixedOrder",
      "discardMode": "discardAfterUse",
      "reshuffleWhenEmpty": false
    }
  ]
}
```

Snapshots must include active card definitions needed by clients:

```json
{
  "cardDefinitions": [
    {
      "cardId": "card_custom_001",
      "deckId": "chance",
      "displayTitle": "Custom movement card",
      "displayText": "Move to a selected tile.",
      "artId": "card_art_placeholder_001",
      "actionType": "MoveToTile",
      "parameters": {
        "targetTileId": "start",
        "collectPassStart": true,
        "resolveLandingTile": false
      },
      "enabled": true
    }
  ]
}
```

Snapshots must include runtime deck state separately from deck and card definitions:

```json
{
  "deckRuntimeState": [
    {
      "deckId": "chance",
      "drawPileCardIds": ["card_custom_002"],
      "discardPileCardIds": ["card_custom_001"]
    }
  ],
  "heldCardIdsByPlayer": {
    "player_1": ["card_escape_001"]
  }
}
```

Projection requirements:

- `rules.version` must change when the rules snapshot shape changes.
- `presetId`, `presetName`, `isCustom`, and resolved `values` must be included.
- Card definitions exposed to clients must match the server-frozen active definitions for the match.
- Runtime pile state must use card IDs, not embedded card records.
- Held card IDs must remain valid against frozen card definitions.
- Clients must not assume built-in preset values during reconnect.

## Planned Protocol

Lobby and pre-game messages:

- `set_rules`: Client requests a complete active rules object or preset selection for the lobby.
- `update_rules`: Client requests a partial rules patch before match start.
- Rule validation errors: Server rejects invalid values with structured field errors.

Future in-game messages:

- `propose_rule_change`: Client proposes a conditional in-game rules patch.
- `rules_updated`: Server broadcasts an accepted rules update with the resolved authoritative rules object.
- `rule_change_rejected`: Server rejects a proposed update with machine-readable reason codes.

Voting placeholders:

- Voting is not implemented in Phase 5.22.
- Future voting must be server-authoritative.
- Future voting must define eligibility, timeout behavior, tie behavior, rejection reasons, and migration rules before any in-game rule or deck mutation is allowed.

## Future Mechanics

Slimer:

- Schema/config-only toggle: `future.slimer.enabled`.
- Future action contract: `ApplyStatusEffect`.
- Future owner: `StatusEffectSystem`.
- Expected configuration fields later may include `statusEffectId`, `durationTurns`, `target`, stacking behavior, immunity rules, and turn timing.
- Not implemented in Phase 5.22.

Earthquake:

- Schema/config-only toggle: `future.earthquake.enabled`.
- Future action contract: `TriggerPropertyDamage`.
- Future owner: `PropertyStateSystem`.
- Expected configuration fields later may include `damageType`, `targetScope`, `repairCost`, property eligibility, rent impact, and repair timing.
- Not implemented in Phase 5.22.

## Non-Goals

- No backend behavior changes.
- No Unity UI.
- No full rules implementation.
- No card execution changes.
- No deck editing implementation.
- No voting implementation beyond placeholders.
- No Slimer implementation.
- No Earthquake implementation.
- No protected wording, branding, board names, card wording, or artwork.
