# MonoJoey Unity Frontend Handover

## Summary

Frontend Chunks 1-3 are implemented in the Unity project at `client-unity/MonoJoey_UnityFrontend`. The completed work is limited to `Assets/Prefabs/` and `Assets/Scripts/`, with runtime validation driven by `AgenticTestRunner` in `SampleScene.unity`.

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

## Optional Visual Polish

- Replace fallback dice number labels with dedicated dice face sprites or icon assets.
- Add turn-active highlight/pulse styling to `TurnUIPrefab`.
- Tune `TokenAnimator` curves and hop height for final board scale.
- Add more player color swatches, turn icons, and tile highlight effects once final art direction is available.
