
# MonoJoey Animation Style

## Goal

MonoJoey must feel buttery-smooth, premium, responsive, and satisfying.

Animation is not decoration. It is core game feedback.

## Camera direction

Primary view:

- Top-down or angled top-down tabletop.
- Mostly fixed and readable.
- Smooth pan/zoom to important events.
- No chaotic free-roam camera.
- No dizzy orbit camera requirement.

The board must be understandable from the default view.

## Animation personality

The game should feel like:

- Premium casino table.
- Luxury digital board game.
- Smooth party-game chaos.
- Dark felt/wood/brass tabletop.

## Required animation systems

### Animation queue

All server events that need visual feedback should enter a client animation queue.

Pattern:

```text
Server event
→ Queue visual action
→ Play animation
→ Update displayed state
→ Continue next event
```

Do not let many important visual events fire at once and become unreadable.

### Dice

- Server decides dice result.
- Client plays dice animation.
- Dice visually settle to server result.
- Do not use client physics as the authoritative result.

### Token movement

- Tokens glide tile-to-tile.
- Use small lift/bounce/settle feel.
- Movement should be readable, not instant teleport, unless fast animation mode is enabled.
- Destination tile should highlight.

### Cards

- Cards should slide/flip into focus.
- Card result should be clear before resolving major consequences.
- Placeholder card art is okay during core phase.

### Auctions

- Auction panel should slide/fade in smoothly.
- Initial 9-second timer should be visually calm and readable.
- After first bid, 3-second countdown should feel tense.
- Every valid bid should reset the visual countdown clearly.
- Highest bidder should be obvious.

### Money/property transactions

- Money transfers should visibly move from payer to receiver or bank.
- Property ownership should stamp/flag/highlight the tile.
- Rent payments should feel impactful.

### Loan Shark events

- Start-of-turn interest collection should be dramatic but not too slow.
- If player is eliminated by interest, the sequence should be clear and satisfying.

## Animation speed modes

Support later:

- Full/Premium.
- Fast.
- Minimal.

Do not block core gameplay waiting on long animations. Events can be skippable/fast-forwardable later.

## Performance targets

- Aim for 60 FPS on normal gaming PCs.
- Avoid heavy particle spam.
- Pool repeated objects like coins, cards, highlights, text bursts.
- UI should feel responsive even during animations.

## Do not do

- Do not make the board hard to read for cinematic shots.
- Do not use huge camera swings every turn.
- Do not allow animation timing to determine server truth.
- Do not require final art before core gameplay works.
