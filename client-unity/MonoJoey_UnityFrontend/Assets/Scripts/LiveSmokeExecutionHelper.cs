using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class LiveSmokeExecutionHelper : MonoBehaviour
{
    private const float DefaultTimeoutSeconds = 8f;
    private const float CooldownSeconds = 2f;
    private const string NotStartedResult = "not_started";

    [SerializeField] private SessionJoinController sessionJoinController;
    [SerializeField] private MonoJoeySessionClient sessionClient;
    [SerializeField] private MonoJoeyBackendMessageRouter messageRouter;
    [SerializeField] private SnapshotHydrator snapshotHydrator;
    [SerializeField] private LiveSessionContext liveSessionContext;
    [SerializeField] private MonoJoeyGameplayCommandDispatcher gameplayCommandDispatcher;
    [SerializeField] private bool createRuntimeControls = true;
    [SerializeField] private float timeoutSeconds = DefaultTimeoutSeconds;
    [SerializeField] private Button runButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button copyReportButton;
    [SerializeField] private Text readinessText;
    [SerializeField] private Text progressText;
    [SerializeField] private Text reportText;

    private readonly List<string> progressLines = new List<string>();
    private bool isSmokeRunning;
    private bool cancellationRequested;
    private int runId;
    private Coroutine activeCoroutine;
    private float lastRunEndedAtRealtime = -1000f;
    private string lastRunResult = NotStartedResult;
    private string lastFailureLabel = "";
    private string lastFailureMessage = "";
    private string lastReportText = "";

    public bool IsSmokeRunning => isSmokeRunning;
    public int RunId => runId;
    public string LastRunResult => lastRunResult;
    public string LastFailureLabel => lastFailureLabel;
    public string LastFailureMessage => lastFailureMessage;
    public string LastReportText => lastReportText;
    public bool RunButtonInteractable => runButton != null && runButton.interactable;
    public bool IsInCooldown => !isSmokeRunning && Time.realtimeSinceStartup - lastRunEndedAtRealtime < CooldownSeconds;

    public void Configure(
        SessionJoinController joinController,
        MonoJoeySessionClient client,
        MonoJoeyBackendMessageRouter router,
        SnapshotHydrator hydrator,
        LiveSessionContext context,
        MonoJoeyGameplayCommandDispatcher dispatcher)
    {
        sessionJoinController = joinController;
        sessionClient = client;
        messageRouter = router;
        snapshotHydrator = hydrator;
        liveSessionContext = context;
        gameplayCommandDispatcher = dispatcher;
        ResolveDependencies();
        EnsureRuntimeControls();
        WireControls();
        RefreshReadiness();
    }

    public void SetTimeoutForValidation(float seconds)
    {
        timeoutSeconds = Mathf.Max(0.1f, seconds);
    }

    public bool TryStartSmokeOnce()
    {
        ResolveDependencies();
        RefreshReadiness();

        if (!CanStartRun(out string reason))
        {
            lastRunResult = "rejected";
            lastFailureLabel = "start_rejected";
            lastFailureMessage = reason;
            AddProgress($"start rejected: {reason}");
            BuildReport("start_rejected", reason, false, false, "");
            RefreshReadiness();
            return false;
        }

        isSmokeRunning = true;
        cancellationRequested = false;
        runId++;
        lastRunResult = "running";
        lastFailureLabel = "";
        lastFailureMessage = "";
        progressLines.Clear();
        AddProgress($"run {runId} started");
        activeCoroutine = StartCoroutine(RunSmokeCoroutine(runId));
        RefreshReadiness();
        return true;
    }

    public void RunSmokeOnceFromPanel()
    {
        TryStartSmokeOnce();
    }

    public void CancelOrCleanupFromPanel()
    {
        CancelSmokeRun("user_cancelled", "Run cancelled by user.");
    }

    public void CopyReportToClipboard()
    {
        GUIUtility.systemCopyBuffer = lastReportText ?? "";
    }

    public void RefreshReadiness()
    {
        ResolveDependencies();
        liveSessionContext?.RefreshFrom(
            sessionClient,
            snapshotHydrator,
            CurrentMode(),
            CurrentBackendUrl(),
            CurrentSessionId(),
            CurrentPlayerId());

        string reason;
        bool canStart = CanStartRun(out reason);
        SetInteractable(runButton, canStart);
        SetInteractable(cancelButton, isSmokeRunning);
        SetInteractable(copyReportButton, !string.IsNullOrWhiteSpace(lastReportText));

        MonoJoeySnapshot snapshot = snapshotHydrator == null ? null : snapshotHydrator.LastSnapshot;
        MonoJoeyTurnSnapshot turn = snapshot == null ? null : snapshot.turn;
        MonoJoeyPlayerSnapshot localPlayer = FindLocalPlayer(snapshot, CurrentPlayerId());
        string auction = snapshot == null || snapshot.activeAuction == null
            ? "auction=--"
            : $"auction={Display(snapshot.activeAuction.propertyTileId)} status={Display(snapshot.activeAuction.status)} highBid={snapshot.activeAuction.highestBid}";
        string cooldown = IsInCooldown ? $"cooldown={(CooldownSeconds - (Time.realtimeSinceStartup - lastRunEndedAtRealtime)):0.0}s" : "cooldown=--";
        string readiness =
            $"live={CurrentMode() == MonoJoeySessionClientMode.LiveBackend} url={Display(CurrentBackendUrl())} session={Display(CurrentSessionId())} player={Display(CurrentPlayerId())}\n" +
            $"connected={Bool(sessionClient != null && sessionClient.IsTransportConnected)} bound={Bool(sessionClient != null && sessionClient.IsBoundToIdentity)} state={(sessionClient == null ? MonoJoeyTransportConnectionState.Idle : sessionClient.State)} fallback={Bool(sessionClient != null && sessionClient.IsUsingMockFallback)}\n" +
            $"snapshot={Bool(snapshotHydrator != null && snapshotHydrator.LastHydrationSucceeded && snapshot != null)} version={(snapshot == null ? 0 : snapshot.snapshotVersion)} snapshotSession={Display(snapshot == null ? "" : snapshot.sessionId)} phase={Display(snapshot == null ? "" : snapshot.phase)} game={Display(snapshot == null ? "" : snapshot.gameStatus)}\n" +
            $"localFound={Bool(localPlayer != null)} currentTurn={Display(turn == null ? "" : turn.currentPlayerId)} flags=rolled:{Bool(turn != null && turn.hasRolledThisTurn)},resolved:{Bool(turn != null && turn.hasResolvedTileThisTurn)},executed:{Bool(turn != null && turn.hasExecutedTileThisTurn)} {auction}\n" +
            $"dispatcher={Bool(gameplayCommandDispatcher != null)} inFlight={Bool(gameplayCommandDispatcher != null && gameplayCommandDispatcher.IsCommandInFlight)} running={Bool(isSmokeRunning)} {cooldown} start={Display(reason)}";

        SetText(readinessText, readiness);
        SetText(progressText, string.Join("\n", progressLines.ToArray()));
        SetText(reportText, string.IsNullOrWhiteSpace(lastReportText) ? "Report: --" : lastReportText);
    }

    private void Awake()
    {
        ResolveDependencies();
        EnsureRuntimeControls();
        WireControls();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        WireControls();
        if (gameplayCommandDispatcher != null)
        {
            gameplayCommandDispatcher.CommandStateChanged += RefreshReadiness;
        }

        if (sessionClient != null)
        {
            sessionClient.StatusChanged += RefreshReadiness;
        }

        RefreshReadiness();
    }

    private void OnDisable()
    {
        if (gameplayCommandDispatcher != null)
        {
            gameplayCommandDispatcher.CommandStateChanged -= RefreshReadiness;
        }

        if (sessionClient != null)
        {
            sessionClient.StatusChanged -= RefreshReadiness;
        }
    }

    private void Update()
    {
        RefreshReadiness();
    }

    private IEnumerator RunSmokeCoroutine(int capturedRunId)
    {
        int baselineErrorCount = messageRouter == null ? 0 : messageRouter.ErrorEnvelopeCount;
        int baselineDirectCount = messageRouter == null ? 0 : messageRouter.DirectCommandResultCount;
        int baselineSnapshotCount = messageRouter == null ? 0 : messageRouter.SnapshotResultCount;
        bool commandSent = false;
        string directResult = "";

        AddProgress("connect/bound check");
        if (sessionClient == null || messageRouter == null || snapshotHydrator == null || gameplayCommandDispatcher == null)
        {
            FinishRun(capturedRunId, "failed", "missing_dependency", "Session, router, hydrator, or dispatcher is missing.", commandSent, directResult);
            yield break;
        }

        if (!sessionClient.IsTransportConnected)
        {
            AddProgress("transport disconnected; Connect() once");
            sessionClient.Connect();
        }
        else if (!sessionClient.IsBoundToIdentity || sessionClient.State != MonoJoeyTransportConnectionState.BoundLive)
        {
            AddProgress("transport connected but unbound; ReconnectSession() once");
            sessionClient.ReconnectSession();
        }

        yield return WaitForBoundLive(capturedRunId, baselineErrorCount);
        if (!IsRunCurrent(capturedRunId))
        {
            yield break;
        }

        AddProgress("bound check passed");
        if (!HasUsableLatestSnapshot())
        {
            int beforeSnapshotRequestCount = messageRouter.SnapshotResultCount;
            AddProgress("snapshot refresh requested once");
            sessionClient.RequestSnapshot();
            yield return WaitForSnapshotResult(capturedRunId, beforeSnapshotRequestCount, baselineErrorCount, "snapshot refresh");
            if (!IsRunCurrent(capturedRunId))
            {
                yield break;
            }
        }
        else
        {
            AddProgress($"snapshot refresh using {Display(snapshotHydrator.LastHydrationSourceMessageType)} hydration");
        }

        string gateLabel;
        string gateMessage;
        if (!ValidatePreRollSafety(out gateLabel, out gateMessage))
        {
            FinishRun(capturedRunId, "blocked", gateLabel, gateMessage, commandSent, directResult);
            yield break;
        }

        AddProgress("command gate passed");
        baselineDirectCount = messageRouter.DirectCommandResultCount;
        baselineSnapshotCount = messageRouter.SnapshotResultCount;
        commandSent = gameplayCommandDispatcher.TryRollDice();
        if (!commandSent)
        {
            FinishRun(capturedRunId, "failed", "send_rejected", Display(gameplayCommandDispatcher.LastCommandError), commandSent, directResult);
            yield break;
        }

        AddProgress("command sent: roll_dice");
        yield return WaitForDirectRollResult(capturedRunId, baselineDirectCount, baselineErrorCount);
        if (!IsRunCurrent(capturedRunId))
        {
            yield break;
        }

        directResult = messageRouter.LastDirectCommandResultType;
        AddProgress($"direct result: {Display(directResult)}");
        if (!string.Equals(directResult, MonoJoeyTransportMessageTypes.RollResult, StringComparison.Ordinal))
        {
            FinishRun(capturedRunId, "failed", "direct_mismatch", $"Expected roll_result, got {Display(directResult)}.", commandSent, directResult);
            yield break;
        }

        if (messageRouter.SnapshotResultCount <= baselineSnapshotCount)
        {
            AddProgress("post-roll snapshot_result missing; RequestSnapshot() once");
            sessionClient.RequestSnapshot();
        }
        else
        {
            AddProgress("post-roll snapshot_result already observed");
        }

        yield return WaitForSnapshotResult(capturedRunId, baselineSnapshotCount, baselineErrorCount, "post-roll authoritative snapshot");
        if (!IsRunCurrent(capturedRunId))
        {
            yield break;
        }

        string validationLabel;
        string validationMessage;
        if (!ValidatePostRollSnapshot(out validationLabel, out validationMessage))
        {
            FinishRun(capturedRunId, "failed", validationLabel, validationMessage, commandSent, directResult);
            yield break;
        }

        AddProgress("final result: post-roll snapshot validated");
        FinishRun(capturedRunId, "passed", "", "Post-roll authoritative snapshot validated.", commandSent, directResult);
    }

    private IEnumerator WaitForBoundLive(int capturedRunId, int baselineErrorCount)
    {
        float deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, timeoutSeconds);
        while (IsRunCurrent(capturedRunId) && Time.realtimeSinceStartup < deadline)
        {
            if (HasTerminalTransportFailure(baselineErrorCount, out string label, out string message))
            {
                FinishRun(capturedRunId, "failed", label, message, false, "");
                yield break;
            }

            if (sessionClient != null
                && sessionClient.State == MonoJoeyTransportConnectionState.BoundLive
                && sessionClient.IsBoundToIdentity
                && snapshotHydrator != null
                && snapshotHydrator.LastHydrationSucceeded)
            {
                yield break;
            }

            yield return null;
        }

        FinishRun(capturedRunId, "failed", "timeout", $"Timed out waiting for BoundLive after {timeoutSeconds:0.0}s.", false, "");
    }

    private IEnumerator WaitForDirectRollResult(int capturedRunId, int baselineDirectCount, int baselineErrorCount)
    {
        float deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, timeoutSeconds);
        while (IsRunCurrent(capturedRunId) && Time.realtimeSinceStartup < deadline)
        {
            if (HasTerminalTransportFailure(baselineErrorCount, out string label, out string message))
            {
                FinishRun(capturedRunId, "failed", label, message, true, "");
                yield break;
            }

            if (messageRouter != null && messageRouter.DirectCommandResultCount > baselineDirectCount)
            {
                yield break;
            }

            yield return null;
        }

        FinishRun(capturedRunId, "failed", "timeout", $"Timed out waiting for roll_result after {timeoutSeconds:0.0}s.", true, "");
    }

    private IEnumerator WaitForSnapshotResult(int capturedRunId, int baselineSnapshotCount, int baselineErrorCount, string label)
    {
        float deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, timeoutSeconds);
        while (IsRunCurrent(capturedRunId) && Time.realtimeSinceStartup < deadline)
        {
            if (HasTerminalTransportFailure(baselineErrorCount, out string failureLabel, out string message))
            {
                FinishRun(capturedRunId, "failed", failureLabel, message, true, messageRouter == null ? "" : messageRouter.LastDirectCommandResultType);
                yield break;
            }

            if (messageRouter != null && messageRouter.SnapshotResultCount > baselineSnapshotCount)
            {
                if (snapshotHydrator != null
                    && snapshotHydrator.LastHydrationSucceeded
                    && string.Equals(snapshotHydrator.LastHydrationSourceMessageType, MonoJoeyTransportMessageTypes.SnapshotResult, StringComparison.Ordinal))
                {
                    yield break;
                }

                FinishRun(capturedRunId, "failed", "missing_snapshot", $"{label}: snapshot_result arrived but hydration failed.", true, messageRouter == null ? "" : messageRouter.LastDirectCommandResultType);
                yield break;
            }

            yield return null;
        }

        FinishRun(capturedRunId, "failed", "timeout", $"Timed out waiting for {label} after {timeoutSeconds:0.0}s.", true, messageRouter == null ? "" : messageRouter.LastDirectCommandResultType);
    }

    private bool CanStartRun(out string reason)
    {
        if (isSmokeRunning || activeCoroutine != null)
        {
            reason = "run_lock_active";
            return false;
        }

        if (IsInCooldown)
        {
            reason = "cooldown";
            return false;
        }

        if (sessionClient == null)
        {
            reason = "missing_session_client";
            return false;
        }

        if (CurrentMode() != MonoJoeySessionClientMode.LiveBackend)
        {
            reason = "live_mode_required";
            return false;
        }

        if (sessionClient.IsUsingMockFallback)
        {
            reason = "mock_fallback_active";
            return false;
        }

        if (!IsValidLiveWebSocketUrl(CurrentBackendUrl()))
        {
            reason = "invalid_live_ws_url";
            return false;
        }

        if (string.IsNullOrWhiteSpace(CurrentSessionId()) || string.IsNullOrWhiteSpace(CurrentPlayerId()))
        {
            reason = "missing_session_or_player";
            return false;
        }

        if (gameplayCommandDispatcher == null)
        {
            reason = "missing_dispatcher";
            return false;
        }

        if (gameplayCommandDispatcher.IsCommandInFlight)
        {
            reason = "dispatcher_in_flight";
            return false;
        }

        reason = "ready";
        return true;
    }

    private bool ValidatePreRollSafety(out string label, out string message)
    {
        if (CurrentMode() != MonoJoeySessionClientMode.LiveBackend)
        {
            label = "blocked:not_live_mode";
            message = "LiveBackend mode is required.";
            return false;
        }

        if (sessionClient == null || sessionClient.IsUsingMockFallback)
        {
            label = "blocked:mock_fallback";
            message = "Mock fallback is active or session client is missing.";
            return false;
        }

        if (!IsValidLiveWebSocketUrl(CurrentBackendUrl()))
        {
            label = "blocked:invalid_url";
            message = $"Live backend URL must be ws/wss and target /ws: {Display(CurrentBackendUrl())}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(CurrentSessionId()) || string.IsNullOrWhiteSpace(CurrentPlayerId()))
        {
            label = "blocked:missing_identity";
            message = "Session ID and player ID are required.";
            return false;
        }

        if (!sessionClient.IsTransportConnected
            || !sessionClient.IsBoundToIdentity
            || sessionClient.State != MonoJoeyTransportConnectionState.BoundLive)
        {
            label = "blocked:not_bound";
            message = $"Transport is not BoundLive: state={sessionClient.State}, connected={sessionClient.IsTransportConnected}, bound={sessionClient.IsBoundToIdentity}.";
            return false;
        }

        if (!HasUsableLatestSnapshot())
        {
            label = "blocked:missing_snapshot";
            message = "No successful matching authoritative snapshot hydration is available.";
            return false;
        }

        MonoJoeySnapshot snapshot = snapshotHydrator.LastSnapshot;
        if (IsCompletedGame(snapshot))
        {
            label = "blocked:game_completed";
            message = $"Game is completed: status={Display(snapshot.status)} gameStatus={Display(snapshot.gameStatus)} winner={Display(snapshot.winnerPlayerId)}.";
            return false;
        }

        MonoJoeyPlayerSnapshot localPlayer = FindLocalPlayer(snapshot, CurrentPlayerId());
        if (localPlayer == null)
        {
            label = "blocked:local_player_missing";
            message = $"Local player {Display(CurrentPlayerId())} was not found in latest snapshot.";
            return false;
        }

        if (localPlayer.isBankrupt || localPlayer.isEliminated)
        {
            label = "blocked:local_player_inactive";
            message = $"Local player is bankrupt={localPlayer.isBankrupt}, eliminated={localPlayer.isEliminated}.";
            return false;
        }

        MonoJoeyTurnSnapshot turn = snapshot.turn;
        if (turn == null)
        {
            label = "blocked:missing_turn";
            message = "Latest snapshot has no turn object.";
            return false;
        }

        if (!string.Equals(turn.currentPlayerId, CurrentPlayerId(), StringComparison.Ordinal))
        {
            label = "blocked:not_local_turn";
            message = $"Current turn player is {Display(turn.currentPlayerId)}, local player is {Display(CurrentPlayerId())}.";
            return false;
        }

        if (snapshot.activeAuction != null)
        {
            label = "blocked:active_auction";
            message = $"Active auction exists for {Display(snapshot.activeAuction.propertyTileId)} status={Display(snapshot.activeAuction.status)} highBid={snapshot.activeAuction.highestBid}.";
            return false;
        }

        if (turn.hasRolledThisTurn)
        {
            label = "blocked:turn_already_rolled";
            message = $"Turn flags block roll: rolled={turn.hasRolledThisTurn}, resolved={turn.hasResolvedTileThisTurn}, executed={turn.hasExecutedTileThisTurn}, phase={Display(snapshot.phase)}.";
            return false;
        }

        if (IsBlockingPhase(snapshot.phase))
        {
            label = "blocked:phase";
            message = $"Phase blocks roll: phase={Display(snapshot.phase)}, rolled={turn.hasRolledThisTurn}, resolved={turn.hasResolvedTileThisTurn}, executed={turn.hasExecutedTileThisTurn}.";
            return false;
        }

        if (gameplayCommandDispatcher == null || gameplayCommandDispatcher.IsCommandInFlight)
        {
            label = "blocked:dispatcher";
            message = gameplayCommandDispatcher == null ? "Dispatcher is missing." : "Dispatcher already has an in-flight command.";
            return false;
        }

        if (!gameplayCommandDispatcher.CanRollDice(out string dispatcherReason))
        {
            label = "blocked:dispatcher_gate";
            message = dispatcherReason;
            return false;
        }

        if (messageRouter != null && messageRouter.ErrorEnvelopeCount > 0)
        {
            label = "blocked:backend_error";
            message = $"Backend error already visible: code={Display(messageRouter.LastErrorCode)} message={Display(messageRouter.LastErrorMessage)}.";
            return false;
        }

        label = "";
        message = "";
        return true;
    }

    private bool ValidatePostRollSnapshot(out string label, out string message)
    {
        if (snapshotHydrator == null || snapshotHydrator.LastSnapshot == null || !snapshotHydrator.LastHydrationSucceeded)
        {
            label = "snapshot_mismatch";
            message = "Post-roll snapshot hydration is missing or failed.";
            return false;
        }

        MonoJoeySnapshot snapshot = snapshotHydrator.LastSnapshot;
        if (!string.Equals(snapshot.sessionId, CurrentSessionId(), StringComparison.Ordinal))
        {
            label = "snapshot_mismatch";
            message = $"Post-roll snapshot session mismatch: expected={Display(CurrentSessionId())}, actual={Display(snapshot.sessionId)}.";
            return false;
        }

        if (FindLocalPlayer(snapshot, CurrentPlayerId()) == null)
        {
            label = "snapshot_mismatch";
            message = $"Post-roll snapshot does not include local player {Display(CurrentPlayerId())}.";
            return false;
        }

        if (!IsCompletedGame(snapshot) && (snapshot.turn == null || !snapshot.turn.hasRolledThisTurn))
        {
            label = "snapshot_mismatch";
            message = "Post-roll authoritative snapshot did not set hasRolledThisTurn.";
            return false;
        }

        label = "";
        message = "";
        return true;
    }

    private void FinishRun(int capturedRunId, string result, string failureLabel, string failureMessage, bool commandSent, string directResult)
    {
        if (capturedRunId != runId)
        {
            return;
        }

        lastRunResult = result ?? "failed";
        lastFailureLabel = failureLabel ?? "";
        lastFailureMessage = failureMessage ?? "";
        AddProgress(string.IsNullOrWhiteSpace(lastFailureLabel)
            ? $"finished: {lastRunResult}"
            : $"finished: {lastRunResult} {lastFailureLabel} {lastFailureMessage}");
        activeCoroutine = null;
        isSmokeRunning = false;
        cancellationRequested = false;
        lastRunEndedAtRealtime = Time.realtimeSinceStartup;
        BuildReport(lastFailureLabel, lastFailureMessage, commandSent, string.Equals(directResult, MonoJoeyTransportMessageTypes.RollResult, StringComparison.Ordinal), directResult);
        RefreshReadiness();
    }

    private void CancelSmokeRun(string label, string message)
    {
        if (!isSmokeRunning && activeCoroutine == null)
        {
            return;
        }

        cancellationRequested = true;
        int capturedRunId = runId;
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }

        FinishRun(capturedRunId, "cancelled", label, message, false, messageRouter == null ? "" : messageRouter.LastDirectCommandResultType);
    }

    private bool IsRunCurrent(int capturedRunId)
    {
        return capturedRunId == runId && isSmokeRunning && !cancellationRequested;
    }

    private bool HasTerminalTransportFailure(int baselineErrorCount, out string label, out string message)
    {
        if (messageRouter != null && messageRouter.ErrorEnvelopeCount > baselineErrorCount)
        {
            label = "backend_error";
            message = $"Backend error code={Display(messageRouter.LastErrorCode)} message={Display(messageRouter.LastErrorMessage)}.";
            return true;
        }

        if (sessionClient == null)
        {
            label = "missing_session_client";
            message = "Session client is missing.";
            return true;
        }

        if (sessionClient.State == MonoJoeyTransportConnectionState.Error)
        {
            label = "transport_error";
            message = $"Transport error: {Display(sessionClient.LastError)}.";
            return true;
        }

        if (sessionClient.State == MonoJoeyTransportConnectionState.Disconnected)
        {
            label = "disconnect";
            message = "Transport disconnected during smoke run.";
            return true;
        }

        label = "";
        message = "";
        return false;
    }

    private bool HasUsableLatestSnapshot()
    {
        return snapshotHydrator != null
            && snapshotHydrator.LastHydrationSucceeded
            && snapshotHydrator.LastSnapshot != null
            && string.Equals(snapshotHydrator.LastSnapshot.sessionId, CurrentSessionId(), StringComparison.Ordinal);
    }

    private void BuildReport(string failureLabel, string failureMessage, bool commandSent, bool directAccepted, string directResult)
    {
        MonoJoeySnapshot snapshot = snapshotHydrator == null ? null : snapshotHydrator.LastSnapshot;
        MonoJoeyTurnSnapshot turn = snapshot == null ? null : snapshot.turn;
        MonoJoeyActiveAuctionSnapshot auction = snapshot == null ? null : snapshot.activeAuction;
        string backendError = messageRouter == null || (string.IsNullOrWhiteSpace(messageRouter.LastErrorCode) && string.IsNullOrWhiteSpace(messageRouter.LastErrorMessage))
            ? "--"
            : $"{Display(messageRouter.LastErrorCode)} {Display(messageRouter.LastErrorMessage)}";

        lastReportText =
            $"UTC: {DateTime.UtcNow:O}\n" +
            $"result: {Display(lastRunResult)}\n" +
            $"failure: {Display(failureLabel)} {Display(failureMessage)}\n" +
            $"url: {Display(CurrentBackendUrl())}\n" +
            $"session: {Display(CurrentSessionId())}\n" +
            $"player: {Display(CurrentPlayerId())}\n" +
            $"state: {(sessionClient == null ? MonoJoeyTransportConnectionState.Idle : sessionClient.State)} connected={Bool(sessionClient != null && sessionClient.IsTransportConnected)} bound={Bool(sessionClient != null && sessionClient.IsBoundToIdentity)} mode={CurrentMode()} fallback={Bool(sessionClient != null && sessionClient.IsUsingMockFallback)}\n" +
            $"snapshot: version={(snapshot == null ? 0 : snapshot.snapshotVersion)} session={Display(snapshot == null ? "" : snapshot.sessionId)} phase={Display(snapshot == null ? "" : snapshot.phase)} gameStatus={Display(snapshot == null ? "" : snapshot.gameStatus)} source={Display(snapshotHydrator == null ? "" : snapshotHydrator.LastHydrationSourceMessageType)} hydrated={Bool(snapshotHydrator != null && snapshotHydrator.LastHydrationSucceeded)}\n" +
            $"turn: index={(turn == null ? -1 : turn.turnIndex)} current={Display(turn == null ? "" : turn.currentPlayerId)} flags=rolled:{Bool(turn != null && turn.hasRolledThisTurn)},resolved:{Bool(turn != null && turn.hasResolvedTileThisTurn)},executed:{Bool(turn != null && turn.hasExecutedTileThisTurn)}\n" +
            $"auction: {AuctionSummary(auction)}\n" +
            $"command: sent={Bool(commandSent)} type={(commandSent ? MonoJoeyTransportMessageTypes.RollDice : "--")} dispatcherInFlight={Bool(gameplayCommandDispatcher != null && gameplayCommandDispatcher.IsCommandInFlight)}\n" +
            $"direct: accepted={Bool(directAccepted)} result={Display(directResult)}\n" +
            $"counts: direct={(messageRouter == null ? 0 : messageRouter.DirectCommandResultCount)} snapshot={(messageRouter == null ? 0 : messageRouter.SnapshotResultCount)} reconnect={(messageRouter == null ? 0 : messageRouter.ReconnectResultCount)} sessionGameplay={(sessionClient == null ? 0 : sessionClient.GameplayCommandRequestCount)}\n" +
            $"lastBackendError: {backendError}";
    }

    private void ResolveDependencies()
    {
        if (sessionJoinController == null)
        {
            sessionJoinController = GetComponent<SessionJoinController>();
        }

        if (sessionJoinController != null)
        {
            if (sessionClient == null)
            {
                sessionClient = sessionJoinController.SessionClient;
            }

            if (messageRouter == null)
            {
                messageRouter = sessionJoinController.MessageRouter;
            }

            if (snapshotHydrator == null)
            {
                snapshotHydrator = sessionJoinController.SnapshotHydrator;
            }

            if (liveSessionContext == null)
            {
                liveSessionContext = sessionJoinController.Context;
            }
        }

        if (liveSessionContext == null)
        {
            liveSessionContext = GetComponent<LiveSessionContext>();
        }

        if (gameplayCommandDispatcher == null && sessionClient != null)
        {
            gameplayCommandDispatcher = sessionClient.GetComponent<MonoJoeyGameplayCommandDispatcher>();
        }

        if (gameplayCommandDispatcher == null)
        {
            gameplayCommandDispatcher = GetComponent<MonoJoeyGameplayCommandDispatcher>();
        }
    }

    private MonoJoeySessionClientMode CurrentMode()
    {
        if (sessionClient != null)
        {
            return sessionClient.Mode;
        }

        if (liveSessionContext != null)
        {
            return liveSessionContext.Mode;
        }

        return sessionJoinController == null ? MonoJoeySessionClientMode.MockValidation : sessionJoinController.SelectedBackendMode;
    }

    private string CurrentBackendUrl()
    {
        if (sessionClient != null)
        {
            return sessionClient.WebSocketUrl ?? "";
        }

        if (liveSessionContext != null)
        {
            return liveSessionContext.BackendUrl ?? "";
        }

        return sessionJoinController == null ? "" : sessionJoinController.CurrentBackendUrl;
    }

    private string CurrentSessionId()
    {
        if (sessionClient != null)
        {
            return sessionClient.SessionId ?? "";
        }

        if (liveSessionContext != null)
        {
            return liveSessionContext.SessionId ?? "";
        }

        return sessionJoinController == null ? "" : sessionJoinController.CurrentSessionId;
    }

    private string CurrentPlayerId()
    {
        if (sessionClient != null)
        {
            return sessionClient.PlayerId ?? "";
        }

        if (liveSessionContext != null)
        {
            return liveSessionContext.PlayerId ?? "";
        }

        return sessionJoinController == null ? "" : sessionJoinController.CurrentPlayerId;
    }

    private void EnsureRuntimeControls()
    {
        if (!createRuntimeControls || runButton != null || GetComponentInParent<Canvas>() == null)
        {
            return;
        }

        Font font = ResolveFont();
        GameObject root = new GameObject("LiveSmokeExecutionHelperControls", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        root.transform.SetParent(transform, false);
        Image background = root.GetComponent<Image>();
        background.color = new Color(0.055f, 0.065f, 0.075f, 0.94f);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(552f, -16f);
        rect.sizeDelta = new Vector2(560f, 500f);

        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 6f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateLabel(root.transform, font, "Live Smoke Helper");
        GameObject row = new GameObject("SmokeButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(root.transform, false);
        row.GetComponent<LayoutElement>().preferredHeight = 30f;
        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 6f;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        runButton = CreateButton(row.transform, font, "Run Live Smoke Once", 170f);
        cancelButton = CreateButton(row.transform, font, "Cancel/Cleanup", 120f);
        copyReportButton = CreateButton(row.transform, font, "Copy Report", 110f);
        readinessText = CreateText(root.transform, font, "Readiness: --", 11, TextAnchor.UpperLeft, new Vector2(536f, 116f));
        progressText = CreateText(root.transform, font, "Progress: --", 11, TextAnchor.UpperLeft, new Vector2(536f, 116f));
        reportText = CreateText(root.transform, font, "Report: --", 11, TextAnchor.UpperLeft, new Vector2(536f, 196f));
    }

    private void WireControls()
    {
        runButton?.onClick.RemoveListener(RunSmokeOnceFromPanel);
        cancelButton?.onClick.RemoveListener(CancelOrCleanupFromPanel);
        copyReportButton?.onClick.RemoveListener(CopyReportToClipboard);
        runButton?.onClick.AddListener(RunSmokeOnceFromPanel);
        cancelButton?.onClick.AddListener(CancelOrCleanupFromPanel);
        copyReportButton?.onClick.AddListener(CopyReportToClipboard);
    }

    private void AddProgress(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        progressLines.Add($"{DateTime.UtcNow:HH:mm:ss} {line}");
        while (progressLines.Count > 10)
        {
            progressLines.RemoveAt(0);
        }

        SetText(progressText, string.Join("\n", progressLines.ToArray()));
    }

    private Font ResolveFont()
    {
        Text existing = GetComponentInChildren<Text>(true);
        if (existing != null && existing.font != null)
        {
            return existing.font;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void CreateLabel(Transform parent, Font font, string value)
    {
        Text label = CreateText(parent, font, value, 15, TextAnchor.MiddleLeft, new Vector2(536f, 24f));
        label.fontStyle = FontStyle.Bold;
    }

    private static Button CreateButton(Transform parent, Font font, string label, float preferredWidth)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.21f, 0.24f, 1f);
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 30f;
        layout.preferredWidth = preferredWidth;
        CreateText(buttonObject.transform, font, label, 12, TextAnchor.MiddleCenter, new Vector2(preferredWidth, 30f));
        return buttonObject.GetComponent<Button>();
    }

    private static Text CreateText(Transform parent, Font font, string value, int fontSize, TextAnchor alignment, Vector2 size)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 0f);
        rect.offsetMax = new Vector2(-6f, 0f);
        rect.sizeDelta = size;

        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.preferredHeight = size.y;

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.text = value;
        return text;
    }

    private static MonoJoeyPlayerSnapshot FindLocalPlayer(MonoJoeySnapshot snapshot, string playerId)
    {
        MonoJoeyPlayerSnapshot[] players = snapshot == null ? null : snapshot.players;
        if (players == null || string.IsNullOrWhiteSpace(playerId))
        {
            return null;
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && string.Equals(players[i].playerId, playerId, StringComparison.Ordinal))
            {
                return players[i];
            }
        }

        return null;
    }

    private static bool IsCompletedGame(MonoJoeySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return false;
        }

        return string.Equals(snapshot.gameStatus, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(snapshot.status, "completed", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(snapshot.winnerPlayerId)
            || !string.IsNullOrWhiteSpace(snapshot.endedAtUtc);
    }

    private static bool IsBlockingPhase(string phase)
    {
        if (string.IsNullOrWhiteSpace(phase))
        {
            return false;
        }

        string normalized = phase.Trim().ToLowerInvariant();
        return normalized.Contains("auction")
            || normalized.Contains("tile")
            || normalized.Contains("complete")
            || normalized.Contains("ended");
    }

    private static bool IsValidLiveWebSocketUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
        {
            return false;
        }

        bool schemeOk = string.Equals(uri.Scheme, "ws", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
        return schemeOk && string.Equals(uri.AbsolutePath, "/ws", StringComparison.OrdinalIgnoreCase);
    }

    private static string AuctionSummary(MonoJoeyActiveAuctionSnapshot auction)
    {
        if (auction == null)
        {
            return "--";
        }

        return $"tile={Display(auction.propertyTileId)} status={Display(auction.status)} highBid={auction.highestBid} highBidder={Display(auction.highestBidderId)} bids={(auction.bids == null ? 0 : auction.bids.Length)}";
    }

    private static void SetInteractable(Selectable selectable, bool interactable)
    {
        if (selectable != null)
        {
            selectable.interactable = interactable;
        }
    }

    private static void SetText(Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }

    private static string Bool(bool value)
    {
        return value ? "true" : "false";
    }
}
