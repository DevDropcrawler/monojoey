# MonoJoey Unity Frontend Handover

## Summary

Frontend Chunks 1-6 are implemented in the Unity project at `client-unity/MonoJoey_UnityFrontend`. The completed work is limited to `Assets/Prefabs/` and `Assets/Scripts/`, with runtime validation driven by an editor Playmode auto-bootstrapped `AgenticTestRunner`.

The backend V1 surface remains frozen. All frontend validation uses local mock/read-only snapshot data only; UI actions log local intent and visual state, but do not mutate or call real backend/server data.

## Workflow Rule

- After each frontend chunk is implemented, validated, committed, and handover updated, push to origin/main unless explicitly told not to.

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

### Chunk 5: Read-Only Snapshot Hydration

- Added `MonoJoeySnapshotModels.cs` with Unity `JsonUtility` DTOs for the authoritative backend snapshot shape, `snapshot_result`, `reconnect_result`, active auctions, board tiles, players, property states, and helper payload hints.
- Added `SnapshotHydrator.cs` as a Unity-only adapter from backend-shaped snapshot JSON/DTOs into existing Chunk 1-4 display controllers.
- Hydration behavior is read-only:
  - selects the configured local player when present, otherwise a non-eliminated player, otherwise the first player,
  - binds `HUDController.PlayerHudSnapshot` and `HUDController.TurnHudSnapshot`,
  - calls `TurnController.BindHudSnapshot(...)` without starting turns, rolling dice, ending turns, or sending requests,
  - binds or clears `AuctionPanelController` from `activeAuction`,
  - binds board tile owner/index state from authoritative `board.tiles`,
  - stops token animation and snaps `PlayerTokenController` to the authoritative `currentTileId`.
- Updated `AuctionPanelController.BindPlayerBidRows(...)` to clear stale rows when a later snapshot has fewer bids or no active auction.
- Extended `AgenticTestRunner.cs` with Chunk 5 validation snapshots:
  - first backend-shaped JSON includes two players, turn flags, board ownership, one property state, active auction bids, and helper payload hints,
  - second backend-shaped JSON sets `activeAuction: null`, omits helper payloads, changes ownership, and moves the local player token,
  - new validation logs include `Read-only snapshot hydration; no backend mutation.`
- No WebSocket client, request DTO, backend mutation, gameplay authority, scene edit, prefab edit, or `ProjectSettings` edit was added.

### Chunk 6: Live Session and Snapshot Hooks

- Extended `SnapshotHydrator.cs` with optional read-only C# events for snapshot start, HUD updates, turn UI updates, auction updates, board tile updates, token snaps, completion, and failure.
- Added `SnapshotHydrator.HydrationHookEvent` with UTC timestamp, hook name, session/player/tile/phase/turn context, success state, and a short message for future live session refresh diagnostics.
- Added `RefreshFromLiveSessionSnapshot(...)` and `RefreshFromLiveSessionSnapshotJson(...)` as semantic wrappers over the existing Chunk 5 hydration path. These do not open sockets, call backend APIs, submit bids, roll dice, or mutate gameplay authority.
- Extended `AgenticTestRunner.cs` to subscribe to all hydrator hooks at runtime and log timestamped hook events.
- Added a Chunk 6 mock live session update after the existing Chunk 5 two-snapshot validation. The live update changes HUD money/loan, turn phase/flags, token authoritative tile, board ownership, and active auction high bidder/high bid rows.
- No prefab structure, scene file, backend code, or `ProjectSettings` change was required.

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
    MonoJoeySnapshotModels.cs
    PlayerTokenController.cs
    SnapshotHydrator.cs
    TokenAnimator.cs
    TurnController.cs
```

## Runtime Validation

- Open/play `Assets/Scenes/SampleScene.unity`.
- `AgenticTestRunner` auto-creates a runtime-only `AgenticTestRunner_RuntimeBootstrap` object for `SampleScene` through `RuntimeInitializeOnLoadMethod`.
- Do not save or commit `SampleScene.unity` just to add `AgenticTestRunner`; the runner is intentionally created from `Assets/Scripts` during Editor Playmode.
- Runtime auto-bootstrap can be disabled by changing `AutoBootstrapEnabled` in `AgenticTestRunner.cs`.
- The runner instantiates the prefabs, binds local mock snapshots, logs serialized-field wiring, and validates:
  - board tile ownership/highlight state,
  - HUD player/turn snapshot display,
  - token movement along a mock path,
  - auction countdown/high-bidder visuals,
  - read-only live session snapshot hooks,
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

### Chunk 5 Runtime Notes

- Expected Chunk 5 logs in `SampleScene` include:
  - `Chunk 5 hydrator configured` with HUD, turn, auction, token, animator, and board tile counts.
  - `Chunk 5 first snapshot hydrated=True` with HUD money/loan/tile, turn phase/flags, active auction high bidder, tile ownership/index, and token snap details.
  - `Chunk 5 second snapshot hydrated=True` with cleared auction id/high bid/high bidder, cleared helper payload counts, changed tile ownership, and token snapped to the replacement authoritative tile.
- The Chunk 5 path only hydrates backend-shaped mock JSON. It does not call `RollDice`, submit bids, send backend mutation requests, or open a WebSocket connection.
- Local compiler validation used Unity's bundled C# compiler against `Assembly-CSharp.csproj` plus the new snapshot scripts and passed with only existing serialized-field assignment warnings. Unity batch-mode play validation could not run in this session because the editor exited during licensing before project load.

### Chunk 6 Runtime Notes

- Expected Chunk 6 logs in `SampleScene` include:
  - `Chunk 6 live session hook subscribed`.
  - timestamped `Chunk 6 hydrator hook` entries for hydration start, HUD, turn, auction, board tile, token, and completion.
  - `Chunk 6 mock session update hydrated=True` with before/after HUD money/loan, turn phase, token tile/index/position, auction high bidder/high bid, and auction tile ownership.
- The mock live update uses `RefreshFromLiveSessionSnapshotJson(...)` against backend-shaped local JSON. It remains read-only and does not call backend networking, `RollDice`, auction submit, scene saves, or `ProjectSettings` edits.
- Local compiler validation used the .NET SDK compiler with Unity runtime references and passed with only existing serialized-field assignment warnings. Unity batch-mode validation was attempted, but existing Unity editor processes prevented a fresh batch log from being produced in this session.

## Optional Visual Polish

- Replace fallback dice number labels with dedicated dice face sprites or icon assets.
- Add turn-active highlight/pulse styling to `TurnUIPrefab`.
- Tune `TokenAnimator` curves and hop height for final board scale.
- Add more player color swatches, turn icons, and tile highlight effects once final art direction is available.
