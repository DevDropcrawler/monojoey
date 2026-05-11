# MonoJoey Unity Frontend Handover

## Summary

Frontend Chunks 1-4 are implemented in the Unity project at `client-unity/MonoJoey_UnityFrontend`. The completed work is limited to `Assets/Prefabs/` and `Assets/Scripts/`, with runtime validation driven by `AgenticTestRunner` in `SampleScene.unity`.

The backend V1 surface remains frozen. All frontend validation uses local mock/read-only snapshot data only; UI actions log local intent and visual state, but do not mutate or call real backend/server data.

## Completed Chunks

### Chunk 1: Auction and Player Token Foundation

- Added `AuctionPanel.prefab` and `PlayerToken.prefab`.
- Added `AuctionPanelController.cs` for local auction snapshot binding, player bid rows, bid request logging, countdown/high-bidder visuals, and local-only bid submission events.
- Added `PlayerTokenController.cs` for player id, token color, current tile index, and world-position movement.
- Added/started `AgenticTestRunner.cs` runtime validation for prefab loading, local mock data binding, and inspector wiring checks.
- Added the `PlayerToken` tag usage on the token prefab.

### Chunk 2: Board, HUD, and Token Animation

- Added `TilePrefab.prefab` and `BoardTileController.cs` for tile id binding, ownership color, highlight state, and token anchor positions.
- Added `HUDPrefab.prefab` and `HUDController.cs` for player HUD and turn HUD snapshot display.
- Added `TokenAnimator.cs` for visual token movement along a tile path with normal, fast, and minimal movement modes.
- Extended `AgenticTestRunner.cs` to instantiate board tiles, bind HUD mock snapshots, animate a token across mock tile ids, and validate auction panel UI animation states.

### Chunk 3: Turn Flow and Dice UI

- Added `TurnUIPrefab.prefab` with current player text, turn status text, roll button, two dice images, dice value labels, and debug log text.
- Added `TurnController.cs` with:
  - Serialized fields: `currentPlayerText`, `turnStatusText`, `rollButton`, `diceImages`, `debugLog`.
  - Public API: `BindSnapshot(TurnHudSnapshot snapshot)`, `StartTurn()`, `EndTurn()`, `RollDice()`.
  - Local-only turn state, roll-button state updates, local dice roll generation, debug logging, and dice animation triggering.
- Added `DiceAnimator.cs` with:
  - Serialized fields: `diceFaces`, `rollDuration`, `rollCurve`.
  - Public API: `AnimateRoll(int[] values)`, `StopAnimation()`.
  - Visual-only dice cycling, settle values, color/scale/rotation feedback, and no backend interaction.
- Extended `AgenticTestRunner.cs` to instantiate the turn UI, bind 1-2 mock `HUDController.TurnHudSnapshot` turns, call `StartTurn()`, `RollDice()`, and `EndTurn()`, log inspector fields and animation state, and continue validating token movement.

### Chunk 4: Mock Turn Interaction Slice

- Extended `TurnController.cs` into a local-only mock turn orchestrator:
  - Runtime mock references for HUD, token animator, board tiles, local player id, and deterministic mock roll values.
  - Read-only validation state for rolling, movement, last movement path, token final position, and latest HUD player snapshot.
  - Coroutine-backed roll flow: roll button gating, dice animation wait, token movement wait, post-roll HUD snapshot refresh, and local debug log entries.
  - Roll button is interactable only for the local current player before rolling and while dice/token animations are idle.
- Extended `TokenAnimator.cs` with validation observability:
  - Last path tile ids, requested step count, completed tile id, final board index, final position, and elapsed step count.
  - Destination tile index now uses `BoardTileController.BoardIndex` when available.
- Extended `BoardTileController.cs` with a local board index used by validation logs and token index reporting.
- Updated `AgenticTestRunner.cs` so the primary `SampleScene` path performs one full mock turn:
  - Instantiates `HUDPrefab`, `TurnUIPrefab`, `PlayerToken`, and the four `TilePrefab` board tiles.
  - Builds `start -> property_01 -> property_02 -> auction_test`.
  - Binds `player-agentic`, starts the turn, rolls deterministic `[1, 2]`, waits for dice and token movement, refreshes HUD, and ends the turn.
  - Logs roll button state, dice values, dice animator state, token path/final position, HUD money/loan/current tile/roll fields, and the turn debug log.
  - Retains auction panel mock validation.
- Prefab updates:
  - `TurnUIPrefab.prefab` serializes the new mock-turn fields with external HUD/token/tiles left null/empty for runtime assignment.
  - `PlayerToken.prefab` now includes a wired `TokenAnimator`.
  - `TilePrefab.prefab` includes default `boardIndex: -1`.

## Folder Structure

Primary frontend assets:

```text
client-unity/MonoJoey_UnityFrontend/Assets/
  Prefabs/
    AuctionPanel.prefab
    HUDPrefab.prefab
    PlayerToken.prefab
    TilePrefab.prefab
    TurnUIPrefab.prefab
  Scripts/
    AgenticTestRunner.cs
    AuctionPanelController.cs
    BoardTileController.cs
    DiceAnimator.cs
    HUDController.cs
    PlayerTokenController.cs
    TokenAnimator.cs
    TurnController.cs
```

## Runtime Validation

- Open/play `Assets/Scenes/SampleScene.unity`.
- `AgenticTestRunner` auto-creates itself for `SampleScene` through `RuntimeInitializeOnLoadMethod`.
- The runner instantiates the prefabs, binds local mock snapshots, logs serialized-field wiring, and validates:
  - board tile ownership/highlight state,
  - HUD player/turn snapshot display,
  - token movement along a mock path,
  - auction countdown/high-bidder visuals,
  - turn UI snapshot binding,
  - dice roll animation,
  - one to two local mock turn flows.
- Expected backend behavior: none. Runtime validation should remain read-only and local.

### Chunk 4 Runtime Notes

- Expected Chunk 4 logs in `SampleScene` include:
  - `Chunk 4 full mock turn start` with `rollButtonInteractable=True`.
  - `Chunk 4 roll triggered` with deterministic dice values `1 + 2` and the roll button disabled while the roll/movement flow runs.
  - `Chunk 4 token path` ending at `auction_test` with `elapsedSteps=3`.
  - `Chunk 4 HUD updated` with changed money, loan, current tile, `hasRolled=True`, and phase `AwaitingTileAction`.
  - `Chunk 4 turn debug log` containing start, roll, dice complete, movement complete, HUD refresh, and end-turn local-only entries.
- `AgenticTestRunner` now chooses `InputSystemUIInputModule` by reflection when the Input System package is active, avoiding legacy input exceptions without changing `ProjectSettings`.
- Backend freeze still applies: no WebSocket client, request DTO, protocol mutation, gameplay authority, scene edit, or `ProjectSettings` edit was added.

## Optional Visual Polish

- Replace fallback dice number labels with dedicated dice face sprites or icon assets.
- Add turn-active highlight/pulse styling to `TurnUIPrefab`.
- Tune `TokenAnimator` curves and hop height for final board scale.
- Add more player color swatches, turn icons, and tile highlight effects once final art direction is available.
