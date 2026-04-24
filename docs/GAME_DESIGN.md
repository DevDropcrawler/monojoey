
# MonoJoey Game Design

## One-line concept

MonoJoey is a PC-first online multiplayer 3D tabletop property-trading game where players battle through auctions, debt, cards, rent, bankruptcy, and customizable rules on a premium top-down animated board.

## Core hook

Create, host, and play chaotic property-trading board games online with configurable rules, cards, auctions, loans, tokens, stats, and later fully custom boards/themes.

## Visual direction

The game should look like a premium tabletop game viewed from above:

- Dark table/felt/casino-inspired board surface.
- Gold/brass trim.
- Clean readable property spaces.
- Smooth token movement.
- Polished card reveals.
- Satisfying dice animations.
- Elegant auction and money-transfer UI.
- Camera stays readable and top-down/angled top-down.

The game should not feel like a free-roam 3D environment. It should feel like a luxury digital board laid out on a table.

## Core gameplay pillars

1. Online multiplayer must work from the start.
2. The server owns the true game state.
3. The Unity client makes server events look beautiful.
4. Every rule should be configurable over time.
5. Mandatory auctions should make properties competitive.
6. Loan Shark mechanics should create brutal economic pressure.
7. Cards should add movement, money, penalty, and status variety.
8. Persistent stats and leaderboards should make every match matter.

## V1 target

V1 is a playable online PC game with placeholder visuals and placeholder card/token content, but with correct architecture.

V1 must include:

- Create/join private online lobby.
- Username entry.
- Placeholder token selection.
- Player color selection.
- Ready state.
- Start match.
- Server-owned dice roll.
- Server-owned movement.
- Server-owned property ownership.
- Mandatory auction mode.
- Auction timers: initial 9s reaction timer, 3s bid-reset timer after first bid.
- Configurable minimum bid increment.
- Loan Shark mode.
- Start-of-turn loan interest payment before rolling.
- Elimination if loan interest bankrupts player.
- Prevention of borrowing to pay loan interest/debt.
- Placeholder Chance-style and Table-style cards based on standard property-game functions.
- Basic bankruptcy and winner resolution.
- Persistent match results and player stats.
- Basic leaderboards.
- Premium animation queue foundation.

## Out of scope for early V1

Do not build these before the playable server-client loop is stable:

- Full custom board editor.
- Steam Workshop.
- Matchmaking.
- Public ranking seasons.
- Final custom card writing.
- Final token models.
- Advanced AI players.
- Mobile/console ports.
- Complex account/social system.
- Cosmetic store.

## Placeholder card policy

During core development, use function-based placeholder cards only:

- `CHANCE_01`, `CHANCE_02`, etc.
- `TABLE_01`, `TABLE_02`, etc.

Do not copy exact Monopoly card wording. Use safe functional descriptions such as:

- Move to Start.
- Move back 3 spaces.
- Pay each opponent.
- Collect from bank.
- Go to lockup.
- Holdable lockup escape.
- Pay per upgrade owned.

The user will retheme and rewrite cards after the core game loop works.

## Placeholder token policy

Use neutral classic tabletop-style placeholders only. Examples:

- Little Car
- Old Boot
- Top Hat
- Small Dog
- Tiny Ship
- Iron
- Thimble
- Wheelbarrow

In code, use neutral placeholder IDs:

- `token_car_placeholder`
- `token_boot_placeholder`
- `token_hat_placeholder`

Later the user will swap these for original custom models.

## Mandatory Auction Mode

When enabled, direct property purchase is disabled.

Flow:

1. Player lands on an unowned property.
2. Auction starts.
3. Initial pre-bid timer begins, default 9 seconds.
4. If no one bids before the initial timer expires, the property remains unowned.
5. After the first valid bid, the auction switches to short countdown mode, default 3 seconds.
6. Every valid new bid resets the short countdown.
7. When short countdown reaches zero, highest bidder wins.
8. Winner pays the bank and receives the property.

Rules editor values:

- `mandatoryAuctionsEnabled`
- `auctionInitialTimerSeconds`
- `auctionBidResetTimerSeconds`
- `auctionMinimumBidIncrement`
- `auctionStartingBid`

## Loan Shark Mode

When enabled, players may borrow emergency money, but interest escalates harshly.

Rules:

1. Player may borrow for eligible obligations/purchases.
2. Interest rate increases as the player borrows more.
3. Interest is charged automatically at the start of that player's next turn before they can roll.
4. If start-of-turn interest bankrupts the player, they are eliminated.
5. Player cannot borrow to pay loan interest, loan principal, or existing loan debt.

Allowed borrow reasons:

- AuctionBid
- RentPayment
- TaxPayment
- CardPenalty
- Fine

Blocked borrow reasons:

- LoanInterest
- LoanPrincipalRepayment
- ExistingLoanDebt

## Stats and leaderboard requirement

The game needs persistent cross-game stats:

- Total games played/completed.
- Wins/losses/win rate.
- Money earned/spent.
- Rent collected/paid.
- Auctions won/lost.
- Biggest auction win.
- Bankruptcies caused/suffered.
- Loan Shark use/debt/interest paid.
- Cards drawn/effects triggered.
- Dice/doubles stats.
- Fastest/longest games.
- Comeback wins.
- Head-to-head stats where possible.

Leaderboards must support detailed categories and filtering later.
