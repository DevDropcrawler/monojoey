# MonoJoey Unity Frontend Handover

## Summary

Frontend Chunks 1-12 are implemented in the Unity project at `client-unity/MonoJoey_UnityFrontend`. The completed work is limited to `Assets/Prefabs/` and `Assets/Scripts/`, with runtime validation driven by an editor Playmode auto-bootstrapped `AgenticTestRunner`.

The backend V1 surface remains frozen. Frontend validation defaults to local mock/read-only snapshot data only. Chunk 11 adds a runtime session join panel for manual live testing; live backend use remains opt-in and the panel sends only connection/recovery requests (`connect`, `disconnect`, `reconnect_session`, and bound `get_snapshot`). Gameplay commands remain routed only through the controlled dispatcher from later turn/auction UI chunks and never locally apply gameplay outcomes. Chunk 12 adds player-facing command feedback for sent, in-flight, direct-result, backend-error, disabled-reason, and waiting-for-authoritative-snapshot states.

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

### Chunk 7: Read-Only Backend Transport Layer

- Added `MonoJoeyTransportMessages.cs` with read-only transport enums, server/error envelope DTOs, and `reconnect_session`/`get_snapshot` request DTOs only.
- Added `MonoJoeyWebSocketTransport.cs`, a thin `ClientWebSocket` wrapper for `/ws` that connects, sends complete JSON text messages, receives complete text messages, and dispatches events back on Unity's main thread.
- Added `MonoJoeyMockTransport.cs`, a deterministic runtime mock transport for Agentic validation. It responds to read-only reconnect/snapshot requests, emits canned errors and broadcasts, and records any forbidden gameplay mutation request type.
- Added `MonoJoeyBackendMessageRouter.cs` to route `snapshot_result` and `reconnect_result` through `SnapshotHydrator`, record advisory event sequence state, log backend `error` envelopes, ignore unknown messages, and ignore sequenced gameplay broadcasts without applying incremental gameplay state.
- Added `MonoJoeySessionClient.cs` as the runtime coordinator for `MockValidation` and `LiveBackend` modes. Public request methods are read-only: `Connect()`, `Disconnect()`, `RequestSnapshot()`, and `ReconnectSession()`.
- Added `MonoJoeyConnectionStatusController.cs` and `ConnectionStatusPanel.prefab` for presentation-only transport status fields.
- Extended `AgenticTestRunner.cs` with Chunk 7 mock transport validation after Chunk 6 hydration validation.
- Extended `SnapshotHydrator.cs` with last hydration source/time observability only.
- No backend protocol files, scenes, `ProjectSettings`, authority model, gameplay prediction, or gameplay mutation requests were changed.

### Chunk 8: Live Backend Integration Validation

- Extended `MonoJoeySessionClient.cs` with bound/unbound identity tracking, reconnect counts, last request timestamps, mode-safe transport switching, and mock fallback visibility.
- Tightened live request behavior:
  - connecting without `sessionId`/`playerId` leaves the socket connected and unbound, with no request sent,
  - connecting with both ids sends `reconnect_session` first,
  - `reconnect_result.payload.snapshot` must hydrate successfully before the client enters `BoundLive`,
  - manual `get_snapshot` is allowed only after the socket is connected and bound to a hydrated identity,
  - backend `error` envelopes are surfaced as errors with no local gameplay compensation.
- Extended `SnapshotHydrator.cs` observability with last hydrated `snapshotVersion`, `sessionId`, `serverNowUtc`, selected player, tile, phase, and turn index. Authoritative hydration stops token movement before refreshing HUD, turn UI, auction, board, and token state.
- Extended `MonoJoeyBackendMessageRouter.cs` with last message time, advisory sequence reporting, hydration-gated binding, and stale/out-of-order sequenced broadcast handling that still avoids incremental gameplay application.
- Extended `MonoJoeyConnectionStatusController.cs` and `ConnectionStatusPanel.prefab` with runtime-only live controls for mode, URL, session id, player id, connect, disconnect, reconnect, and get snapshot. The status panel now renders mode, bound state, fallback warning, last request, last message, sequences, reconnect/request counters, snapshot observability, and backend error code/message.
- Added disabled-by-default `MonoJoeySessionClient.ExperimentalDebug*` wrappers for `roll_dice`, `end_turn`, and `place_bid`. These wrappers are not wired into gameplay UI or default validation; they only send intent payloads when `enableExperimentalMutationRequests` is explicitly enabled.
- Extended `AgenticTestRunner.cs` with Chunk 8 mock checks for default mock mode, missing identity connection, reconnect hydration observability, manual snapshot hydration, stale/out-of-order broadcast handling, disconnect/reconnect transitions, and zero default mutation requests.
- Added an optional live backend smoke path in `AgenticTestRunner.cs`, disabled by default and requiring runtime URL/session/player values.
- No backend files, scenes, `ProjectSettings`, gameplay prediction, local money/ownership mutation, turn-button mutation wiring, or backend protocol changes were added.

## Folder Structure

Primary frontend assets:

```text
client-unity/MonoJoey_UnityFrontend/Assets/
  Prefabs/
    AuctionPanel.prefab
    ConnectionStatusPanel.prefab
    HUDPrefab.prefab
    PlayerToken.prefab
    SessionJoinPanel.prefab
    TilePrefab.prefab
    TurnUIPrefab.prefab
  Scripts/
    AgenticTestRunner.cs
    AuctionPanelController.cs
    BoardTileController.cs
    DiceAnimator.cs
    HUDController.cs
    MonoJoeyBackendMessageRouter.cs
    MonoJoeyConnectionStatusController.cs
    MonoJoeyGameplayCommandDispatcher.cs
    MonoJoeyMockTransport.cs
    MonoJoeySessionClient.cs
    MonoJoeySnapshotModels.cs
    MonoJoeyTransportMessages.cs
    MonoJoeyWebSocketTransport.cs
    PlayerTokenController.cs
    SessionJoinController.cs
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
  - read-only backend transport routing and mock recovery,
  - live backend binding/reconnect validation in mock mode,
  - session join panel field validation, button gating, reconnect, disconnect, and snapshot recovery,
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

### Chunk 7 Runtime Notes

- Expected Chunk 7 logs in `SampleScene` include:
  - `Chunk 7 mock transport connected`.
  - `reconnect_result routed to SnapshotHydrator` and `Chunk 7 mock transport connected` with `reconnectHydrated=1`.
  - `snapshot_result routed to SnapshotHydrator` and `Chunk 7 snapshot_result hydrated through SnapshotHydrator`.
  - `backend error envelope` with `mock_backend_error`.
  - `Sequenced broadcast ignored` and a debounced read-only `get_snapshot` when bound.
  - `Chunk 7 connection status updated`.
  - `Chunk 7 no gameplay mutation request sent`.
- Live backend mode uses `MonoJoeyWebSocketTransport` only. If `sessionId` and `playerId` are set, connect sends `reconnect_session`; otherwise it remains connected/unbound and sends no request. Auto-reconnect hydrates from `reconnect_result.payload.snapshot` and does not replay missed events.
- Mock fallback in live mode is disabled unless `allowMockFallbackOnLiveFailure` is explicitly enabled; when enabled it logs that the UI is no longer live backend state.
- Validation results for this handoff:
  - `dotnet build Assembly-CSharp.csproj -v minimal` could not run because the local machine is missing the .NET Framework 4.7.1 targeting pack (`MSB3644`).
  - A direct Roslyn compile against Unity runtime references passed for all `Assets/Scripts/*.cs`, with only existing serialized-field assignment warnings.
  - Unity batch validation was attempted with `Unity.exe -batchmode -quit -projectPath ...`; the log reached licensing/project-path setup and then exited before project load with return code 1. Existing Unity editor processes were active, so Play Mode `AgenticTestRunner` log confirmation was not available in this session.

### Chunk 8 Runtime Notes

- Expected Chunk 8 mock logs in `SampleScene` include:
  - `Chunk 8 missing session/player connect` with connected/unbound state and `sentRequests=0`.
  - `Chunk 8 mock transport connected/reconnect hydrated` with `BoundLive`, `reconnectHydrated=1`, and advisory sequence.
  - `Chunk 8 reconnect observability` with snapshot version, session, server time, player, tile, phase, and turn.
  - `Chunk 8 manual snapshot_result hydrated after reconnect`.
  - `Chunk 8 stale/out-of-order sequenced broadcasts ignored/read-only refresh`.
  - `Chunk 8 experimental mutation default guard` with zero experimental and mock mutation requests.
  - `Chunk 8 disconnect transition` followed by `Chunk 8 reconnect transition`.
- Optional live smoke is disabled by default. When runtime URL/session/player are provided and `runChunk8LiveBackendSmoke` is enabled, expected logs are connect/reconnecting, `reconnect_result` hydrated to `BoundLive`, optional `get_snapshot`, `snapshot_result` hydrated, disconnect to `Disconnected`, and reconnect back to `BoundLive`.
- Authority boundary: backend snapshots and direct backend responses are recovery truth. Unity does not predict dice, money, ownership, auctions, or turn outcomes; stale/out-of-order broadcasts are never applied incrementally.
- Validation results for this handoff:
  - `dotnet build Assembly-CSharp.csproj -v minimal` still cannot run because the local machine is missing the .NET Framework 4.7.1 targeting pack (`MSB3644`).
  - Direct Roslyn compile against Unity runtime references passed for all `Assets/Scripts/*.cs`, with only serialized-field assignment warnings.
  - Unity batch/play validation was not started because three existing `Unity` editor processes were already active for the local machine/project, so a clean project load was not available in this session.

### Chunk 9 Runtime Notes

- Added `MonoJoeyGameplayCommandDispatcher` as the only Unity gameplay intent dispatcher for `roll_dice`, `resolve_tile`, `execute_tile`, `end_turn`, and `place_bid`.
- Request payloads contain only `sessionId`/`playerId`, plus positive `amount` for `place_bid`; Unity does not serialize dice values, movement, money, tile outcomes, auction IDs, winners, or local result data.
- `MonoJoeySessionClient` now exposes a narrow gameplay command send gateway with command counters and live/bound state checks. The prior Chunk 8 experimental debug mutation wrappers were removed.
- `MonoJoeyBackendMessageRouter` recognizes `roll_result`, `resolve_tile_result`, `execute_tile_result`, `end_turn_result`, and `bid_result` as direct command results. These clear matching dispatcher in-flight state and update logs/status only; gameplay UI still hydrates from `snapshot_result` and `reconnect_result`.
- `TurnController` and `AuctionPanelController` preserve existing mock validation behavior by default. When explicitly configured for live command dispatch, roll/bid clicks send intent only and do not locally roll dice, animate movement, mutate HUD money, advance turns, or update auction high-bid state.
- `MonoJoeyMockTransport` still rejects/counts gameplay mutation requests in normal `MockValidation`. Explicit dispatcher test mode returns canned direct command results for isolated validation.
- Optional live smoke remains disabled by default. If enabled with URL/session/player, it binds live state first; `runChunk9LiveRollSmoke` must also be enabled before the runner sends a single `roll_dice`.
- Validation results for this handoff:
  - `dotnet build client-unity/MonoJoey_UnityFrontend/Assembly-CSharp.csproj -v minimal` could not run because the local machine is missing the .NET Framework 4.7.1 targeting pack (`MSB3644`).
  - Direct Roslyn compile against Unity runtime references passed for all `Assets/Scripts/*.cs`, with only serialized-field assignment warnings.
  - Unity batch validation was attempted, but existing Unity editor processes were active and no clean batch log was produced.

### Chunk 10 Runtime Notes

- `TurnUIPrefab` now serializes real `Resolve`, `Execute`, and `End Turn` buttons alongside the existing `Roll` button.
- `TurnController` live command mode now gates `roll_dice`, `resolve_tile`, `execute_tile`, and `end_turn` from hydrated authoritative turn flags, bound identity, dispatcher in-flight state, local current-player identity, and active-auction presence.
- `resolve_tile` is enabled only after the authoritative snapshot says the local player rolled and has not resolved. `execute_tile` is enabled only after resolve and before execute. `end_turn` is enabled only after roll/resolve/execute and when the authoritative snapshot has no active auction.
- `SnapshotHydrator` passes only active-auction presence into turn action gating. Unity still does not infer or mutate game authority.
- `AuctionPanelController` keeps bid state snapshot-driven, disables live bids when unbound/in-flight/no-active-auction/invalid amount, and allows active-auction bids without requiring local turn ownership.
- `MonoJoeyGameplayCommandDispatcher` now exposes typed `Can*` checks, last blocked command/reason, command-attempt logs, direct-result logs, and in-flight transition logs.
- `AgenticTestRunner` adds a Chunk 10 mock-live command flow in explicit dispatcher test mode: hydrate initial snapshot, click roll, verify direct result does not mutate UI, hydrate post-roll, send resolve, hydrate active auction, send bid, send execute, hydrate no-auction post-execute, send end turn, and hydrate next-player turn with local buttons disabled.
- Validation results for this handoff:
  - Direct Roslyn compile against Unity/Mono runtime references passed for all `Assets/Scripts/*.cs`, with only serialized-field assignment warnings.
  - `dotnet build client-unity/MonoJoey_UnityFrontend/Assembly-CSharp.csproj -v minimal` is still blocked by missing .NET Framework 4.7.1 targeting pack (`MSB3644`).
  - Unity batch/play validation was not started because three existing `Unity` editor processes were already active, blocking a clean project load.

### Chunk 11 Runtime Notes

- Added `SessionJoinPanel.prefab` and `SessionJoinController.cs` as a focused runtime-only connection panel for manual live/mock session testing.
- The panel exposes backend URL, session ID, player ID, mode selection (`MockValidation` / `LiveBackend`), and `Connect`, `Disconnect`, `Reconnect`, and `Get Snapshot` buttons.
- Button gating is based on validated fields plus `MonoJoeySessionClient` state: empty inputs block connect, reconnect requires a connected transport and identity, and get snapshot requires a bound hydrated identity.
- The panel does not reference `MonoJoeyGameplayCommandDispatcher` and never sends gameplay commands. It only applies form values to `MonoJoeySessionClient` and calls connection/recovery methods.
- `AgenticTestRunner` now instantiates `SessionJoinPanel` at runtime and validates empty-input blocking, valid mock connect/hydration, manual snapshot, reconnect, disconnect state updates, and zero gameplay mutation requests.
- Optional Chunk 11 live smoke is disabled by default and requires URL/session/player values before connecting to a real backend.
- Scenes still should not be saved; the panel is instantiated by runtime validation and can be manually placed later only when scene ownership is explicitly in scope.
- Validation results for this handoff:
  - `git diff --check` passed for the Chunk 11 script/prefab changes with only existing line-ending warnings.
  - `dotnet build client-unity/MonoJoey_UnityFrontend/Assembly-CSharp.csproj -v minimal` remains blocked by missing .NET Framework 4.7.1 targeting pack (`MSB3644`).
  - Direct Unity/Mono compiler validation could not be completed because this machine's standalone compiler reference set conflicted before project code compilation.
  - Unity batch/play validation was not started because three existing `Unity` editor processes were active, blocking a clean project load.

### Chunk 12 Runtime Notes

- `MonoJoeyGameplayCommandDispatcher` now exposes command feedback state and a `CommandStateChanged` event: last direct result type/summary/time, backend error code/message, blocked command/reason, in-flight command, and waiting-for-authoritative-snapshot state.
- Direct command results clear matching in-flight commands and set `IsWaitingForAuthoritativeSnapshot`; only `snapshot_result`, `reconnect_result`, backend error, disconnect/transport error, timeout, or a new command clears that waiting state.
- `TurnController` now renders a compact command feedback text block on `TurnUIPrefab` for last command, in-flight command, direct result or backend error, authoritative-snapshot wait, current disabled reason, and active-auction status context.
- Turn button disabled reasons are player-facing and remain intent-only: not connected, not bound, not local turn, missing prior turn step, already completed step, active auction for `end_turn`, or command in flight.
- `AuctionPanelController` reuses its log text as bid feedback, exposes `LastBidFeedbackText`, and surfaces invalid amount, no active auction, not connected/bound/live, command in flight, and backend rejection/error messages. It still does not change auction high bid, money, ownership, or auction state from command success.
- `MonoJoeyConnectionStatusController` command status now includes waiting-for-snapshot, latest blocked command/reason, direct result type, and backend command error details.
- `MonoJoeyMockTransport` has a validation-only hook to hold canned command results so in-flight UI states can be observed deterministically.
- `AgenticTestRunner` adds a Chunk 12 mock-live feedback pass after the Chunk 10 command flow. It logs normal `MockValidation` blocking, no-connection/unbound/not-local/turn-flag/active-auction/in-flight disabled reasons, bid feedback states, direct-result non-mutation before snapshot, snapshot-only UI hydration, and backend error visibility.
- Validation results for this handoff:
  - `git diff --check` passed with line-ending warnings only.
  - Direct Roslyn compile using the Unity 6000.4.6f1 generated project references passed for all `Assets/Scripts/*.cs`, with serialized-field assignment warnings only.
  - `dotnet build client-unity/MonoJoey_UnityFrontend/Assembly-CSharp.csproj -v minimal` remains blocked by missing .NET Framework 4.7.1 targeting pack (`MSB3644`).
  - Unity batch/play validation was not started because three existing `Unity` editor processes were active, blocking a clean project load.

## Optional Visual Polish

- Replace fallback dice number labels with dedicated dice face sprites or icon assets.
- Add turn-active highlight/pulse styling to `TurnUIPrefab`.
- Tune `TokenAnimator` curves and hop height for final board scale.
- Add more player color swatches, turn icons, and tile highlight effects once final art direction is available.
