
# MonoJoey Technical Architecture

## Final stack decision

- Engine: Unity.
- Client language: C#.
- Platform: PC first.
- Backend: C#/.NET.
- Multiplayer model: server-authoritative.
- Realtime transport: WebSockets for V1.
- Database: PostgreSQL after core game server is stable.
- Animation tooling: Unity animation queue, DOTween if added later, Cinemachine-style camera control if added later.

## Core architecture rule

Server owns truth. Client owns theatre.

The server decides:

- Turn order.
- Dice results.
- Player movement.
- Property ownership.
- Rent.
- Auctions.
- Bids.
- Loan Shark debt and interest.
- Card effects.
- Bankruptcy.
- Match end.
- Stats.

The Unity client displays:

- Board scene.
- Tokens.
- Dice animation.
- Card animation.
- Auction UI.
- Money transfer UI.
- Camera movement.
- Lobby UI.
- Editor UI later.

## Suggested repo layout

```text
MonoJoey/
  client-unity/
    Assets/
      Scripts/
        Board/
        Animation/
        UI/
        Networking/
        Audio/
        EditorTools/
      Scenes/
      Prefabs/
      Art/
  server-dotnet/
    MonoJoey.Server/
      Game/
        Rules/
        Turns/
        Auctions/
        Loans/
        Cards/
        Properties/
        Players/
        Stats/
      Realtime/
      Lobbies/
      Persistence/
    MonoJoey.Server.Tests/
  shared/
    MonoJoey.Shared/
      Protocol/
      Schemas/
      Dtos/
  docs/
  tools/
```

## Project layering

### Shared layer

Contains DTOs/protocol types that can be used by server and possibly by Unity client.

Examples:

- `ClientMessageType`
- `ServerEventType`
- `PlayerId`
- `MatchId`
- `TileId`
- `CardId`
- `RulesetConfig`
- `BoardConfig`

### Server game layer

Pure C# domain logic. Should be testable without Unity.

Contains:

- Match state.
- Board state.
- Player state.
- Rules config.
- Turn engine.
- Auction state machine.
- Loan Shark service.
- Card resolver.
- Property/rent resolver.
- Bankruptcy resolver.
- Stats aggregator.

### Server realtime layer

Handles WebSocket connections, lobby membership, client messages, server events, reconnect identity, and broadcasting.

### Unity client networking layer

Connects to server, sends player intentions, receives server events, and hands events to animation queue/state presenter.

### Unity presentation layer

Never decides authoritative game outcomes.

It animates and displays server results.

## Server event to animation pattern

Server broadcasts:

```json
{
  "type": "DiceRolled",
  "matchId": "match_123",
  "playerId": "player_1",
  "dice": [4, 2],
  "fromTileId": "tile_7",
  "toTileId": "tile_13"
}
```

Client flow:

```text
Receive server event
→ Add visual action to animation queue
→ Play dice animation
→ Move token
→ Highlight destination
→ Apply/refresh displayed state
```

## Client message policy

The client sends intentions only.

Allowed examples:

- `CreateLobbyRequest`
- `JoinLobbyRequest`
- `SetPlayerProfileRequest`
- `SetReadyRequest`
- `RollDiceRequest`
- `PlaceBidRequest`
- `TakeLoanRequest`
- `UseCardRequest`
- `EndTurnRequest`

Forbidden examples:

- Client sets dice results.
- Client sets final money.
- Client sets final property owner.
- Client declares bankruptcy result.
- Client declares auction winner.

## Database timing

Do not add PostgreSQL before the core in-memory server works.

Order:

1. In-memory match engine.
2. In-memory lobby server.
3. In-memory stats/match summaries.
4. Persistence abstraction.
5. PostgreSQL implementation.

## Testing strategy

Server first. Unit-test domain systems without Unity.

Required tests:

- Dice range and deterministic injection.
- Movement and pass-start handling.
- Property landing and auction start.
- Auction timers and bid validation.
- Loan interest calculation.
- Borrow reason allow/block logic.
- Start-of-turn interest bankruptcy.
- Card resolver actions.
- Match end/winner calculation.
- Stats aggregation.

Unity tests come later after core protocol is stable.

## Reasoning-level guidance for Codex

- Low/medium: UI-only placeholder screens or docs-only updates.
- Medium: normal server feature chunks with tests.
- High: auction state machine, loan anti-loop, bankruptcy, protocol contracts, persistence/stat schemas.
- XHigh: any change that alters architecture, trust boundaries, saved data shape, or multiplayer authority model.
