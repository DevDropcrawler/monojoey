# MonoJoey – Future Game Modes & Expansion Ideas

## Purpose

This document captures future gameplay mode ideas, expansion mechanics, and optional systems for MonoJoey. These ideas are intentionally deferred until after the core multiplayer MVP is stable.

Core principle:
All future gameplay systems must remain:

* server authoritative
* deterministic
* reconnect-safe
* modular
* toggleable
* rules-engine driven
* compatible with Classic Mode

No future mode should permanently contaminate the classic gameplay ruleset.

---

# Core Long-Term Design Direction

MonoJoey should eventually support multiple rulesets and game styles:

* Classic Mode
* Chaos Mode
* Zombie Mode
* Persistent Progression Mode
* Tournament Mode
* Fast/Casual Modes
* Community Presets
* Custom Rule Presets

The long-term architecture should treat these as:

* modular rulesets
* modifier pipelines
* event systems
* optional gameplay layers

NOT hardcoded gameplay forks.

---

# Rules Engine Philosophy

All future mechanics should ideally plug into:

* Rules Engine
* Modifier System
* Temporary Status Effect System
* Event Pipeline
* Card Effect Framework
* Snapshot/Reconnect System

This preserves:

* multiplayer sync stability
* reconnect reliability
* deterministic gameplay
* Classic Mode purity
* easier future balancing

---

# Zombie Horde Mode (Future Expansion)

## Status

Future expansion mechanic.
Not part of MVP.

## High-Level Concept

A special card/event spawns a zombie horde behind the player that pulled the card.

The player must outrun the horde around the board.

If the horde catches the player:

* penalty occurs
* money loss or temporary status effect
* possibly reduced movement or forced payments

Escaping condition:

* passing GO
* surviving a full lap
* or reaching a defined safe zone

## Intended Feel

* tension
* chase mechanic
* spectator excitement
* temporary high-pressure event
* memorable multiplayer moments

## Proposed Mechanics (Initial Concept)

### Spawn

* Horde spawns 8–10 tiles behind affected player.

### Movement

Potential approaches:

* player reduced to one die
* zombie horde rolls separately
* horde advances each turn phase
* horde may move after affected player finishes turn

### Catch Condition

If horde reaches same tile:

* penalty applied
* event may continue or end

### Escape Condition

Passing GO outruns the horde and ends the event.

## Important Architecture Notes

Zombie horde must eventually be:

* backend authoritative
* temporary board entity/state
* snapshot-safe
* reconnect-safe
* fully deterministic

Frontend responsibilities:

* visualizing horde
* animations
* effects
* UI warnings

Frontend must NOT:

* calculate horde movement
* decide catches
* resolve penalties

## Ruleset Support

Zombie mechanics must be:

* fully toggleable
* disabled in Classic Mode
* compatible with custom presets

---

# Persistent Property Progression Mode

## Status

Future expansion mechanic.
Not part of MVP.

## High-Level Concept

Properties evolve over many games based on player history and usage.

Example:
A player frequently buys a specific property group or earns significant money from a property over multiple matches.

That property may:

* level up
* visually evolve
* gain minor bonuses
* unlock prestige/cosmetic states

## Important Goal

This system must avoid:

* pay-to-win feeling
* massive stat imbalance
* destroying Classic Mode fairness

Bonuses should remain:

* small
* capped
* transparent
* optional

---

# Suggested Direction: Property Affinity / Mastery

Instead of huge stat boosts, use:

* property affinity
* property mastery
* prestige evolution

Examples:

* tiny rent modifiers (+2% to +5%)
* reduced upgrade costs
* cosmetic evolution tiers
* unique board visuals
* animated landmarks
* special sound effects
* minor mortgage benefits
* limited protection from negative events

The best rewards may ultimately be:

* cosmetic prestige
* visual evolution
* status/identity
  rather than raw power.

---

# Persistent Progression Architecture

Persistent progression should eventually exist outside active matches.

Recommended structure:

* player profile progression
* optional loadout/modifier layer
* validated by backend
* applied only when ruleset allows

The active match should remain:

* deterministic
* ruleset-driven
* fully authoritative

---

# Custom Rules & Toggle Philosophy

Every future system should support:

* ON/OFF toggles
* preset integration
* lobby/game configuration
* reconnect-safe serialization

Examples:

* Zombie Horde Enabled
* Persistent Progression Enabled
* Chaos Events Enabled
* Fast Auctions
* Loan Shark Enabled
* Earthquake System Enabled
* Slimer Enabled

---

# Long-Term Future Expansion Possibilities

## Chaos Event Decks

Temporary world/game events:

* market crash
* tax spike
* blackout
* inflation
* random property boom
* transport shutdown

## Dynamic Board Conditions

Temporary tile modifiers:

* boosted rent zones
* dangerous zones
* protected zones
* event hotspots

## Seasonal / Community Modes

Rotating presets:

* Halloween zombie mode
* ultra-chaos weekends
* fast-match presets
* limited-event rulesets

## Prestige / Cosmetic Systems

Non-gameplay progression:

* animated boards
* token skins
* custom dice
* evolved property visuals
* player banners/titles

---

# Important Future Safety Rule

No future expansion should:

* break reconnect stability
* bypass snapshot authority
* introduce frontend authority
* create desync-prone simulation
* hardcode mode-specific logic into the core turn loop

All systems should remain:

* modular
* rules-engine based
* snapshot-driven
* backend authoritative
* toggleable
* deterministic

# MonoJoey – Future Game Modes & Expansion Ideas

## Purpose

This document captures future gameplay mode ideas, expansion mechanics, and optional systems for MonoJoey. These ideas are intentionally deferred until after the core multiplayer MVP is stable.

Core principle:
All future gameplay systems must remain:

* server authoritative
* deterministic
* reconnect-safe
* modular
* toggleable
* rules-engine driven
* compatible with Classic Mode

No future mode should permanently contaminate the classic gameplay ruleset.

---

# Core Long-Term Design Direction

MonoJoey should eventually support multiple rulesets and game styles:

* Classic Mode
* Chaos Mode
* Zombie Mode
* Persistent Progression Mode
* Tournament Mode
* Fast/Casual Modes
* Community Presets
* Custom Rule Presets

The long-term architecture should treat these as:

* modular rulesets
* modifier pipelines
* event systems
* optional gameplay layers

NOT hardcoded gameplay forks.

---

# Rules Engine Philosophy

All future mechanics should ideally plug into:

* Rules Engine
* Modifier System
* Temporary Status Effect System
* Event Pipeline
* Card Effect Framework
* Snapshot/Reconnect System

This preserves:

* multiplayer sync stability
* reconnect reliability
* deterministic gameplay
* Classic Mode purity
* easier future balancing

---

# Zombie Horde Mode (Future Expansion)

## Status

Future expansion mechanic.
Not part of MVP.

## High-Level Concept

A special card/event spawns a zombie horde behind the player that pulled the card.

The player must outrun the horde around the board.

If the horde catches the player:

* penalty occurs
* money loss or temporary status effect
* possibly reduced movement or forced payments

Escaping condition:

* passing GO
* surviving a full lap
* or reaching a defined safe zone

## Intended Feel

* tension
* chase mechanic
* spectator excitement
* temporary high-pressure event
* memorable multiplayer moments

## Proposed Mechanics (Initial Concept)

### Spawn

* Horde spawns 8–10 tiles behind affected player.

### Movement

Potential approaches:

* player reduced to one die
* zombie horde rolls separately
* horde advances each turn phase
* horde may move after affected player finishes turn

### Catch Condition

If horde reaches same tile:

* penalty applied
* event may continue or end

### Escape Condition

Passing GO outruns the horde and ends the event.

## Important Architecture Notes

Zombie horde must eventually be:

* backend authoritative
* temporary board entity/state
* snapshot-safe
* reconnect-safe
* fully deterministic

Frontend responsibilities:

* visualizing horde
* animations
* effects
* UI warnings

Frontend must NOT:

* calculate horde movement
* decide catches
* resolve penalties

## Ruleset Support

Zombie mechanics must be:

* fully toggleable
* disabled in Classic Mode
* compatible with custom presets

---

# Persistent Property Progression Mode

## Status

Future expansion mechanic.
Not part of MVP.

## High-Level Concept

Properties evolve over many games based on player history and usage.

Example:
A player frequently buys a specific property group or earns significant money from a property over multiple matches.

That property may:

* level up
* visually evolve
* gain minor bonuses
* unlock prestige/cosmetic states

## Important Goal

This system must avoid:

* pay-to-win feeling
* massive stat imbalance
* destroying Classic Mode fairness

Bonuses should remain:

* small
* capped
* transparent
* optional

---

# Suggested Direction: Property Affinity / Mastery

Instead of huge stat boosts, use:

* property affinity
* property mastery
* prestige evolution

Examples:

* tiny rent modifiers (+2% to +5%)
* reduced upgrade costs
* cosmetic evolution tiers
* unique board visuals
* animated landmarks
* special sound effects
* minor mortgage benefits
* limited protection from negative events

The best rewards may ultimately be:

* cosmetic prestige
* visual evolution
* status/identity
  rather than raw power.

---

# Persistent Progression Architecture

Persistent progression should eventually exist outside active matches.

Recommended structure:

* player profile progression
* optional loadout/modifier layer
* validated by backend
* applied only when ruleset allows

The active match should remain:

* deterministic
* ruleset-driven
* fully authoritative

---

# Custom Rules & Toggle Philosophy

Every future system should support:

* ON/OFF toggles
* preset integration
* lobby/game configuration
* reconnect-safe serialization

Examples:

* Zombie Horde Enabled
* Persistent Progression Enabled
* Chaos Events Enabled
* Fast Auctions
* Loan Shark Enabled
* Earthquake System Enabled
* Slimer Enabled

---

# Long-Term Future Expansion Possibilities

## Chaos Event Decks

Temporary world/game events:

* market crash
* tax spike
* blackout
* inflation
* random property boom
* transport shutdown

## Dynamic Board Conditions

Temporary tile modifiers:

* boosted rent zones
* dangerous zones
* protected zones
* event hotspots

## Seasonal / Community Modes

Rotating presets:

* Halloween zombie mode
* ultra-chaos weekends
* fast-match presets
* limited-event rulesets

## Prestige / Cosmetic Systems

Non-gameplay progression:

* animated boards
* token skins
* custom dice
* evolved property visuals
* player banners/titles

---

# Important Future Safety Rule

No future expansion should:

* break reconnect stability
* bypass snapshot authority
* introduce frontend authority
* create desync-prone simulation
* hardcode mode-specific logic into the core turn loop

All systems should remain:

* modular
* rules-engine based
* snapshot-driven
* backend authoritative
* toggleable
* deterministic
