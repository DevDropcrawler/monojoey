
# MonoJoey Placeholder Ledger

This file tracks intentional placeholders so they are not mistaken for final design.

## Placeholder policy

Placeholders are allowed while building the core game, but they must be:

- Clearly named as placeholders in code/data.
- Safe/legal/neutral.
- Easy to replace later.
- Listed here when introduced.

## Current placeholders

### Card names and text

Status: INTENTIONAL CORE-GAME PLACEHOLDER.

Use functional placeholder cards only:

- `CHANCE_01` through `CHANCE_16`
- `TABLE_01` through `TABLE_16`

Do not copy exact Monopoly card wording.

Replacement later:

- User will rewrite/rename/retheme all cards after core game loop is working.

### Token models

Status: INTENTIONAL CORE-GAME PLACEHOLDER.

Use neutral tabletop-style placeholder tokens:

- `token_car_placeholder`
- `token_boot_placeholder`
- `token_hat_placeholder`
- `token_dog_placeholder`
- `token_ship_placeholder`
- `token_iron_placeholder`
- `token_thimble_placeholder`
- `token_wheelbarrow_placeholder`

Replacement later:

- User will swap in original custom models.

### Board art

Status: PLACEHOLDER UNTIL PREMIUM ART PASS.

Allowed during core:

- Simple square board.
- Plain property spaces.
- Basic colored groups.
- Placeholder card decks.
- Placeholder currency.

Final direction:

- Premium top-down/angled tabletop.
- Dark felt/casino-table look.
- Gold/brass trim.
- Smooth shadows and readable spaces.

### Currency and property names

Status: PLACEHOLDER.

Use neutral names:

- `Placeholder Property 01`
- `Placeholder Property 02`
- `Default Currency`

Do not use Monopoly property names.

### UI style

Status: EARLY PLACEHOLDER.

Gameplay UI must eventually be premium and smooth. Early UI may be plain if it supports the server-client loop.

## Placeholder update rule

Every chunk that introduces a new placeholder must update this file.
