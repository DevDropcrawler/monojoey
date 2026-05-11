using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TurnController : MonoBehaviour
{
    [Header("Turn UI")]
    [SerializeField] private Text currentPlayerText;
    [SerializeField] private Text turnStatusText;
    [SerializeField] private Button rollButton;
    [SerializeField] private Button resolveButton;
    [SerializeField] private Button executeButton;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private Image[] diceImages;
    [SerializeField] private Text commandFeedbackText;
    [SerializeField] private Text debugLog;

    [Header("Mock Turn Orchestration")]
    [SerializeField] private HUDController hudController;
    [SerializeField] private TokenAnimator tokenAnimator;
    [SerializeField] private BoardTileController[] boardTiles = Array.Empty<BoardTileController>();
    [SerializeField] private string localPlayerId = "player-agentic";
    [SerializeField] private bool useMockRollValues = true;
    [SerializeField] private int[] mockRollValues = { 1, 2 };

    [Header("Live Gameplay Commands")]
    [SerializeField] private bool useLiveGameplayCommandDispatcher;
    [SerializeField] private MonoJoeyGameplayCommandDispatcher gameplayCommandDispatcher;

    private DiceAnimator diceAnimator;
    private HUDController.TurnHudSnapshot lastSnapshot;
    private HUDController.PlayerHudSnapshot lastHudPlayerSnapshot;
    private bool hasSnapshot;
    private bool hasHudPlayerSnapshot;
    private bool isTurnActive;
    private bool hasRolledLocally;
    private bool isMovementAnimating;
    private int[] lastRollValues = Array.Empty<int>();
    private string[] lastMovementPath = Array.Empty<string>();
    private Vector3 lastTokenPosition;
    private Coroutine activeRollRoutine;
    private bool hasActiveAuctionFromHydration;
    private string lastRollBlockedReason = "";
    private string lastResolveBlockedReason = "";
    private string lastExecuteBlockedReason = "";
    private string lastEndTurnBlockedReason = "";

    public HUDController.TurnHudSnapshot LastSnapshot => lastSnapshot;
    public bool HasSnapshot => hasSnapshot;
    public bool IsTurnActive => isTurnActive;
    public bool HasRolledLocally => hasRolledLocally;
    public bool IsRolling => activeRollRoutine != null;
    public bool IsMovementAnimating => isMovementAnimating || (tokenAnimator != null && tokenAnimator.IsAnimating);
    public int[] LastRollValues => lastRollValues;
    public IReadOnlyList<string> LastMovementPath => lastMovementPath;
    public Vector3 LastTokenPosition => lastTokenPosition;
    public HUDController.PlayerHudSnapshot LastHudPlayerSnapshot => lastHudPlayerSnapshot;
    public bool UseLiveGameplayCommandDispatcher => useLiveGameplayCommandDispatcher;
    public int DiceImageCount => diceImages == null ? 0 : diceImages.Length;
    public bool RollButtonInteractable => rollButton != null && rollButton.interactable;
    public bool ResolveButtonInteractable => resolveButton != null && resolveButton.interactable;
    public bool ExecuteButtonInteractable => executeButton != null && executeButton.interactable;
    public bool EndTurnButtonInteractable => endTurnButton != null && endTurnButton.interactable;
    public bool HasActiveAuctionFromHydration => hasActiveAuctionFromHydration;
    public string LastRollBlockedReason => lastRollBlockedReason;
    public string LastResolveBlockedReason => lastResolveBlockedReason;
    public string LastExecuteBlockedReason => lastExecuteBlockedReason;
    public string LastEndTurnBlockedReason => lastEndTurnBlockedReason;
    public string DebugLogText => debugLog == null ? "" : debugLog.text;
    public string CommandFeedbackText => commandFeedbackText == null ? "" : commandFeedbackText.text;

    private void Awake()
    {
        diceAnimator = GetComponent<DiceAnimator>();
        if (diceAnimator == null)
        {
            diceAnimator = GetComponentInChildren<DiceAnimator>();
        }

        if (rollButton != null)
        {
            rollButton.onClick.AddListener(RollDice);
        }

        if (resolveButton != null)
        {
            resolveButton.onClick.AddListener(ResolveTile);
        }

        if (executeButton != null)
        {
            executeButton.onClick.AddListener(ExecuteTile);
        }

        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(EndTurn);
        }

        SubscribeDispatcherFeedback();
        RefreshUi();
    }

    private void OnDestroy()
    {
        UnsubscribeDispatcherFeedback();

        if (rollButton != null)
        {
            rollButton.onClick.RemoveListener(RollDice);
        }

        if (resolveButton != null)
        {
            resolveButton.onClick.RemoveListener(ResolveTile);
        }

        if (executeButton != null)
        {
            executeButton.onClick.RemoveListener(ExecuteTile);
        }

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(EndTurn);
        }
    }

    public void ConfigureMockTurnReferences(
        HUDController hud,
        TokenAnimator token,
        BoardTileController[] tiles,
        string validationLocalPlayerId)
    {
        hudController = hud;
        tokenAnimator = token;
        boardTiles = tiles ?? Array.Empty<BoardTileController>();

        if (!string.IsNullOrWhiteSpace(validationLocalPlayerId))
        {
            localPlayerId = validationLocalPlayerId;
        }

        RefreshUi();
        AppendDebugLog($"Runtime mock refs assigned: hud={hudController != null}, token={tokenAnimator != null}, tiles={boardTiles.Length}, localPlayer={DisplayText(localPlayerId, "--")}.");
    }

    public void ConfigureLiveGameplayCommandDispatcher(MonoJoeyGameplayCommandDispatcher dispatcher, string validationLocalPlayerId)
    {
        UnsubscribeDispatcherFeedback();
        gameplayCommandDispatcher = dispatcher;
        useLiveGameplayCommandDispatcher = dispatcher != null;
        SubscribeDispatcherFeedback();

        if (!string.IsNullOrWhiteSpace(validationLocalPlayerId))
        {
            localPlayerId = validationLocalPlayerId;
        }

        RefreshUi();
        AppendDebugLog($"Live command dispatcher configured: enabled={useLiveGameplayCommandDispatcher}, localPlayer={DisplayText(localPlayerId, "--")}.");
    }

    public void BindSnapshot(HUDController.TurnHudSnapshot snapshot)
    {
        lastSnapshot = snapshot;
        hasSnapshot = true;
        isTurnActive = false;
        hasRolledLocally = snapshot.HasRolledThisTurn;
        RefreshUi();
        AppendDebugLog($"Snapshot bound locally: player={DisplayText(snapshot.CurrentPlayerId, "--")}, turn={snapshot.TurnIndex}, phase={DisplayText(snapshot.Phase, "Unknown")}.");
    }

    public void BindHudSnapshot(HUDController.PlayerHudSnapshot player, HUDController.TurnHudSnapshot turn)
    {
        lastHudPlayerSnapshot = player;
        hasHudPlayerSnapshot = true;
        BindSnapshot(turn);

        if (hudController != null)
        {
            hudController.BindSnapshot(player, turn);
        }

        AppendDebugLog($"HUD snapshot cached locally: player={DisplayText(player.PlayerId, "--")}, money={player.Money}, loan={player.LoanTotalBorrowed}, tile={DisplayText(player.CurrentTileId, "--")}.");
    }

    public void BindTurnActionContext(bool hasActiveAuction)
    {
        hasActiveAuctionFromHydration = hasActiveAuction;
        RefreshUi();
        AppendDebugLog($"Turn action context hydrated: activeAuction={hasActiveAuctionFromHydration}.");
    }

    public void StartTurn()
    {
        isTurnActive = true;
        hasRolledLocally = hasSnapshot && lastSnapshot.HasRolledThisTurn;
        RefreshUi();
        AppendDebugLog($"StartTurn local UI state: player={CurrentPlayerId()}, localPlayer={DisplayText(localPlayerId, "--")}, turn={TurnIndexText()}.");
    }

    public void EndTurn()
    {
        if (UseLiveCommandMode())
        {
            if (!CanEndTurnLive(out string reason))
            {
                AppendDebugLog($"Live end_turn ignored: {reason}.");
                RefreshUi();
                return;
            }

            bool sent = gameplayCommandDispatcher.TryEndTurn();
            AppendDebugLog(sent
                ? $"Live end_turn command sent: localRequestId={DisplayText(gameplayCommandDispatcher.LastCommandLocalRequestId, "--")}."
                : $"Live end_turn command rejected: {DisplayText(gameplayCommandDispatcher.LastCommandError, "--")}.");
            RefreshUi();
            return;
        }

        isTurnActive = false;
        RefreshUi();
        AppendDebugLog($"EndTurn local UI state: player={CurrentPlayerId()}, lastRoll={FormatRoll(lastRollValues)}.");
    }

    public void RollDice()
    {
        if (UseLiveCommandMode())
        {
            if (!CanRollLive(out string reason))
            {
                AppendDebugLog($"Live roll ignored: {reason}.");
                RefreshUi();
                return;
            }

            bool sent = gameplayCommandDispatcher.TryRollDice();
            AppendDebugLog(sent
                ? $"Live roll command sent: localRequestId={DisplayText(gameplayCommandDispatcher.LastCommandLocalRequestId, "--")}."
                : $"Live roll command rejected: {DisplayText(gameplayCommandDispatcher.LastCommandError, "--")}.");
            RefreshUi();
            return;
        }

        RollDiceRoutine();
    }

    public void ResolveTile()
    {
        if (!UseLiveCommandMode())
        {
            AppendDebugLog("Resolve tile ignored because mock turn validation has no local resolve action.");
            RefreshUi();
            return;
        }

        if (!CanResolveTileLive(out string reason))
        {
            AppendDebugLog($"Live resolve_tile ignored: {reason}.");
            RefreshUi();
            return;
        }

        bool sent = gameplayCommandDispatcher.TryResolveTile();
        AppendDebugLog(sent
            ? $"Live resolve_tile command sent: localRequestId={DisplayText(gameplayCommandDispatcher.LastCommandLocalRequestId, "--")}."
            : $"Live resolve_tile command rejected: {DisplayText(gameplayCommandDispatcher.LastCommandError, "--")}.");
        RefreshUi();
    }

    public void ExecuteTile()
    {
        if (!UseLiveCommandMode())
        {
            AppendDebugLog("Execute tile ignored because mock turn validation has no local execute action.");
            RefreshUi();
            return;
        }

        if (!CanExecuteTileLive(out string reason))
        {
            AppendDebugLog($"Live execute_tile ignored: {reason}.");
            RefreshUi();
            return;
        }

        bool sent = gameplayCommandDispatcher.TryExecuteTile();
        AppendDebugLog(sent
            ? $"Live execute_tile command sent: localRequestId={DisplayText(gameplayCommandDispatcher.LastCommandLocalRequestId, "--")}."
            : $"Live execute_tile command rejected: {DisplayText(gameplayCommandDispatcher.LastCommandError, "--")}.");
        RefreshUi();
    }

    public Coroutine RollDiceRoutine()
    {
        if (UseLiveCommandMode())
        {
            AppendDebugLog("Local roll routine ignored because live command mode is active.");
            RefreshUi();
            return null;
        }

        if (!CanRoll(out string reason))
        {
            AppendDebugLog($"Roll ignored: {reason}.");
            RefreshUi();
            return null;
        }

        activeRollRoutine = StartCoroutine(RunRollDiceRoutine());
        RefreshUi();
        return activeRollRoutine;
    }

    private IEnumerator RunRollDiceRoutine()
    {
        lastRollValues = NextRollValues();
        hasRolledLocally = true;
        RefreshUi();
        AppendDebugLog($"Roll accepted locally: values={FormatRoll(lastRollValues)}.");

        if (diceAnimator != null)
        {
            diceAnimator.AnimateRoll(lastRollValues);
            AppendDebugLog($"Dice animation started locally: values={FormatRoll(lastRollValues)}.");

            while (diceAnimator.IsAnimating)
            {
                RefreshUi();
                yield return null;
            }
        }

        AppendDebugLog($"Dice animation complete locally: values={FormatRoll(lastRollValues)}.");

        yield return AnimateMockTokenMovement(Sum(lastRollValues));
        ApplyPostRollHudSnapshot();

        activeRollRoutine = null;
        RefreshUi();
    }

    private IEnumerator AnimateMockTokenMovement(int diceTotal)
    {
        lastMovementPath = BuildMockMovementPath(diceTotal);
        if (lastMovementPath.Length == 0 || tokenAnimator == null || boardTiles == null || boardTiles.Length == 0)
        {
            lastTokenPosition = tokenAnimator == null ? lastTokenPosition : tokenAnimator.transform.position;
            AppendDebugLog($"Movement skipped locally: path={FormatPath(lastMovementPath)}, tokenAnimator={tokenAnimator != null}, boardTiles={(boardTiles == null ? 0 : boardTiles.Length)}.");
            yield break;
        }

        Dictionary<string, BoardTileController> tilesById = BuildTilesById();
        int movementSteps = Mathf.Max(0, lastMovementPath.Length - 1);
        isMovementAnimating = true;
        RefreshUi();
        AppendDebugLog($"Movement animation started locally: path={FormatPath(lastMovementPath)}, steps={movementSteps}.");

        tokenAnimator.AnimateAlongPath(lastMovementPath, tilesById, movementSteps, TokenAnimator.MovementKind.Normal);
        while (tokenAnimator != null && tokenAnimator.IsAnimating)
        {
            RefreshUi();
            yield return null;
        }

        isMovementAnimating = false;
        lastTokenPosition = tokenAnimator == null ? lastTokenPosition : tokenAnimator.FinalPosition;
        AppendDebugLog($"Movement animation complete locally: finalTile={FinalMovementTileId()}, finalBoardIndex={(tokenAnimator == null ? -1 : tokenAnimator.FinalTileIndex)}, position={lastTokenPosition}.");
    }

    private void ApplyPostRollHudSnapshot()
    {
        HUDController.PlayerHudSnapshot previousPlayer = hasHudPlayerSnapshot
            ? lastHudPlayerSnapshot
            : CreateDefaultPlayerSnapshot();

        string finalTileId = FinalMovementTileId();
        int diceTotal = Sum(lastRollValues);
        lastHudPlayerSnapshot = new HUDController.PlayerHudSnapshot(
            previousPlayer.PlayerId,
            previousPlayer.Username,
            previousPlayer.ColorId,
            previousPlayer.Money - (diceTotal * 10),
            finalTileId,
            previousPlayer.LoanTotalBorrowed + (diceTotal * 5),
            previousPlayer.LoanInterestRatePercent,
            previousPlayer.LoanNextTurnInterestDue + diceTotal,
            previousPlayer.LoanTier,
            previousPlayer.IsBankrupt,
            previousPlayer.IsEliminated,
            previousPlayer.IsLockedUp);
        hasHudPlayerSnapshot = true;

        lastSnapshot = new HUDController.TurnHudSnapshot(
            string.IsNullOrWhiteSpace(lastSnapshot.CurrentPlayerId) ? previousPlayer.PlayerId : lastSnapshot.CurrentPlayerId,
            lastSnapshot.TurnIndex,
            "AwaitingTileAction",
            true,
            false,
            false);
        hasSnapshot = true;

        if (hudController != null)
        {
            hudController.BindSnapshot(lastHudPlayerSnapshot, lastSnapshot);
        }

        AppendDebugLog($"HUD snapshot refreshed locally: money={lastHudPlayerSnapshot.Money}, loan={lastHudPlayerSnapshot.LoanTotalBorrowed}, tile={DisplayText(lastHudPlayerSnapshot.CurrentTileId, "--")}, hasRolled={lastSnapshot.HasRolledThisTurn}, phase={lastSnapshot.Phase}.");
    }

    private bool CanRoll(out string reason)
    {
        if (UseLiveCommandMode())
        {
            return CanRollLive(out reason);
        }

        if (!isTurnActive)
        {
            reason = "no local active turn";
            return false;
        }

        if (!hasSnapshot)
        {
            reason = "no local turn snapshot";
            return false;
        }

        if (!string.Equals(lastSnapshot.CurrentPlayerId, localPlayerId, StringComparison.Ordinal))
        {
            reason = $"current player {DisplayText(lastSnapshot.CurrentPlayerId, "--")} is not local player {DisplayText(localPlayerId, "--")}";
            return false;
        }

        if (hasRolledLocally)
        {
            reason = "local turn already rolled";
            return false;
        }

        if (IsRolling)
        {
            reason = "dice routine already running";
            return false;
        }

        if (diceAnimator != null && diceAnimator.IsAnimating)
        {
            reason = "dice animation already running";
            return false;
        }

        if (IsMovementAnimating)
        {
            reason = "token movement already running";
            return false;
        }

        reason = "";
        return true;
    }

    private bool CanRollLive(out string reason)
    {
        if (!CanUseLiveTurnCommand(MonoJoeyTransportMessageTypes.RollDice, out reason))
        {
            return false;
        }

        if (lastSnapshot.HasRolledThisTurn)
        {
            reason = "already rolled";
            return false;
        }

        if (!CanSendLiveTurnCommand(MonoJoeyTransportMessageTypes.RollDice, out reason))
        {
            return false;
        }

        reason = "";
        return true;
    }

    private bool CanResolveTileLive(out string reason)
    {
        if (!CanUseLiveTurnCommand(MonoJoeyTransportMessageTypes.ResolveTile, out reason))
        {
            return false;
        }

        if (!lastSnapshot.HasRolledThisTurn)
        {
            reason = "roll first";
            return false;
        }

        if (lastSnapshot.HasResolvedTileThisTurn)
        {
            reason = "tile already resolved";
            return false;
        }

        if (!CanSendLiveTurnCommand(MonoJoeyTransportMessageTypes.ResolveTile, out reason))
        {
            return false;
        }

        reason = "";
        return true;
    }

    private bool CanExecuteTileLive(out string reason)
    {
        if (!CanUseLiveTurnCommand(MonoJoeyTransportMessageTypes.ExecuteTile, out reason))
        {
            return false;
        }

        if (!lastSnapshot.HasRolledThisTurn)
        {
            reason = "roll first";
            return false;
        }

        if (!lastSnapshot.HasResolvedTileThisTurn)
        {
            reason = "resolve tile first";
            return false;
        }

        if (lastSnapshot.HasExecutedTileThisTurn)
        {
            reason = "tile already executed";
            return false;
        }

        if (!CanSendLiveTurnCommand(MonoJoeyTransportMessageTypes.ExecuteTile, out reason))
        {
            return false;
        }

        reason = "";
        return true;
    }

    private bool CanEndTurnLive(out string reason)
    {
        if (!CanUseLiveTurnCommand(MonoJoeyTransportMessageTypes.EndTurn, out reason))
        {
            return false;
        }

        if (!lastSnapshot.HasRolledThisTurn)
        {
            reason = "roll first";
            return false;
        }

        if (!lastSnapshot.HasResolvedTileThisTurn)
        {
            reason = "resolve tile first";
            return false;
        }

        if (!lastSnapshot.HasExecutedTileThisTurn)
        {
            reason = "execute tile first";
            return false;
        }

        if (hasActiveAuctionFromHydration)
        {
            reason = "active auction must finish first";
            return false;
        }

        if (!CanSendLiveTurnCommand(MonoJoeyTransportMessageTypes.EndTurn, out reason))
        {
            return false;
        }

        reason = "";
        return true;
    }

    private bool CanUseLiveTurnCommand(string requestType, out string reason)
    {
        _ = requestType;
        if (gameplayCommandDispatcher == null)
        {
            reason = "live command dispatcher is not configured";
            return false;
        }

        if (!gameplayCommandDispatcher.IsGameplayCommandModeAvailable)
        {
            reason = "not connected to a live gameplay session";
            return false;
        }

        if (!gameplayCommandDispatcher.IsTransportConnected)
        {
            reason = "not connected";
            return false;
        }

        if (!gameplayCommandDispatcher.IsBoundToIdentity)
        {
            reason = "not bound to session/player";
            return false;
        }

        if (!hasSnapshot)
        {
            reason = "no authoritative turn snapshot";
            return false;
        }

        if (!string.Equals(lastSnapshot.CurrentPlayerId, localPlayerId, StringComparison.Ordinal))
        {
            reason = $"not local turn: current player {DisplayText(lastSnapshot.CurrentPlayerId, "--")}";
            return false;
        }

        reason = "";
        return true;
    }

    private bool CanSendLiveTurnCommand(string requestType, out string reason)
    {
        if (gameplayCommandDispatcher == null)
        {
            reason = "live command dispatcher is not configured";
            return false;
        }

        if (gameplayCommandDispatcher.IsCommandInFlight)
        {
            reason = $"command in flight: {DisplayText(gameplayCommandDispatcher.InFlightRequestType, "--")}";
            return false;
        }

        if (!gameplayCommandDispatcher.CanSendCommand(requestType, out reason))
        {
            return false;
        }

        reason = "";
        return true;
    }

    private void RefreshUi()
    {
        if (currentPlayerText != null)
        {
            currentPlayerText.text = hasSnapshot
                ? $"Current: {DisplayText(lastSnapshot.CurrentPlayerId, "--")}"
                : "Current: --";
        }

        if (turnStatusText != null)
        {
            string phase = hasSnapshot ? DisplayText(lastSnapshot.Phase, "Unknown") : "No Snapshot";
            string turnIndex = TurnIndexText();
            string rollState = IsRolling ? "rolling" : IsMovementAnimating ? "moving" : hasRolledLocally ? "rolled" : "ready";
            string activeState = isTurnActive ? "active" : "idle";
            turnStatusText.text = $"Turn {turnIndex}: {phase} | {activeState} | {rollState}";
        }

        if (rollButton != null)
        {
            rollButton.interactable = CanRollForUi();
        }

        if (resolveButton != null)
        {
            resolveButton.interactable = CanResolveForUi();
        }

        if (executeButton != null)
        {
            executeButton.interactable = CanExecuteForUi();
        }

        if (endTurnButton != null)
        {
            endTurnButton.interactable = CanEndTurnForUi();
        }

        RefreshCommandFeedbackText();
    }

    private bool CanRollForUi()
    {
        if (UseLiveCommandMode())
        {
            bool allowed = CanRollLive(out string reason);
            lastRollBlockedReason = allowed ? "" : reason;
            return allowed;
        }

        bool canRoll = isTurnActive
            && hasSnapshot
            && string.Equals(lastSnapshot.CurrentPlayerId, localPlayerId, StringComparison.Ordinal)
            && !hasRolledLocally
            && !IsRolling
            && (diceAnimator == null || !diceAnimator.IsAnimating)
            && !IsMovementAnimating;
        lastRollBlockedReason = canRoll ? "" : "mock roll prerequisites are not met";
        return canRoll;
    }

    private bool CanResolveForUi()
    {
        if (!UseLiveCommandMode())
        {
            lastResolveBlockedReason = "live dispatcher mode is off";
            return false;
        }

        bool allowed = CanResolveTileLive(out string reason);
        lastResolveBlockedReason = allowed ? "" : reason;
        return allowed;
    }

    private bool CanExecuteForUi()
    {
        if (!UseLiveCommandMode())
        {
            lastExecuteBlockedReason = "live dispatcher mode is off";
            return false;
        }

        bool allowed = CanExecuteTileLive(out string reason);
        lastExecuteBlockedReason = allowed ? "" : reason;
        return allowed;
    }

    private bool CanEndTurnForUi()
    {
        if (!UseLiveCommandMode())
        {
            lastEndTurnBlockedReason = "live dispatcher mode is off";
            return false;
        }

        bool allowed = CanEndTurnLive(out string reason);
        lastEndTurnBlockedReason = allowed ? "" : reason;
        return allowed;
    }

    private int[] NextRollValues()
    {
        int[] source = useMockRollValues && mockRollValues != null && mockRollValues.Length > 0
            ? mockRollValues
            : new[]
            {
                UnityEngine.Random.Range(1, 7),
                UnityEngine.Random.Range(1, 7),
            };

        int[] values = new int[Mathf.Max(2, source.Length)];
        for (int i = 0; i < values.Length; i++)
        {
            int sourceValue = i < source.Length ? source[i] : 1;
            values[i] = Mathf.Clamp(sourceValue, 1, 6);
        }

        return values;
    }

    private string[] BuildMockMovementPath(int diceTotal)
    {
        if (boardTiles == null || boardTiles.Length == 0)
        {
            return Array.Empty<string>();
        }

        int startIndex = ResolveStartTileIndex();
        int steps = Mathf.Max(0, diceTotal);
        string[] path = new string[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            BoardTileController tile = boardTiles[(startIndex + i) % boardTiles.Length];
            path[i] = tile == null ? "" : tile.TileId;
        }

        return path;
    }

    private int ResolveStartTileIndex()
    {
        string currentTileId = hasHudPlayerSnapshot ? lastHudPlayerSnapshot.CurrentTileId : "";
        if (!string.IsNullOrWhiteSpace(currentTileId) && boardTiles != null)
        {
            for (int i = 0; i < boardTiles.Length; i++)
            {
                if (boardTiles[i] != null && string.Equals(boardTiles[i].TileId, currentTileId, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        return 0;
    }

    private Dictionary<string, BoardTileController> BuildTilesById()
    {
        Dictionary<string, BoardTileController> tilesById = new Dictionary<string, BoardTileController>();
        if (boardTiles == null)
        {
            return tilesById;
        }

        foreach (BoardTileController tile in boardTiles)
        {
            if (tile != null && !string.IsNullOrWhiteSpace(tile.TileId))
            {
                tilesById[tile.TileId] = tile;
            }
        }

        return tilesById;
    }

    private HUDController.PlayerHudSnapshot CreateDefaultPlayerSnapshot()
    {
        return new HUDController.PlayerHudSnapshot(
            DisplayText(localPlayerId, "player-agentic"),
            "Agentic Player",
            "gold",
            1500,
            boardTiles != null && boardTiles.Length > 0 && boardTiles[0] != null ? boardTiles[0].TileId : "start",
            0,
            10f,
            0,
            "starter",
            false,
            false,
            false);
    }

    private void AppendDebugLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string normalizedMessage = UseLiveCommandMode()
            ? $"{message} Backend authoritative; no local gameplay state mutation."
            : $"{message} Mock/read-only; no backend mutation.";
        if (debugLog != null)
        {
            debugLog.text = string.IsNullOrWhiteSpace(debugLog.text) ? normalizedMessage : $"{debugLog.text}\n{normalizedMessage}";
        }

        Debug.Log($"[TurnController] {normalizedMessage}", this);
    }

    private bool UseLiveCommandMode()
    {
        return useLiveGameplayCommandDispatcher && gameplayCommandDispatcher != null;
    }

    private void SubscribeDispatcherFeedback()
    {
        if (gameplayCommandDispatcher != null)
        {
            gameplayCommandDispatcher.CommandStateChanged += HandleCommandStateChanged;
        }
    }

    private void UnsubscribeDispatcherFeedback()
    {
        if (gameplayCommandDispatcher != null)
        {
            gameplayCommandDispatcher.CommandStateChanged -= HandleCommandStateChanged;
        }
    }

    private void HandleCommandStateChanged()
    {
        RefreshUi();
    }

    private void RefreshCommandFeedbackText()
    {
        if (commandFeedbackText == null)
        {
            return;
        }

        if (!UseLiveCommandMode())
        {
            commandFeedbackText.text = "Command: mock/read-only";
            return;
        }

        List<string> lines = new List<string>();
        string lastCommand = DisplayText(gameplayCommandDispatcher.LastCommandRequestType, "--");
        string lastRequestId = DisplayText(gameplayCommandDispatcher.LastCommandLocalRequestId, "--");
        lines.Add($"Last: {lastCommand} id={lastRequestId}");

        if (gameplayCommandDispatcher.IsCommandInFlight)
        {
            lines.Add($"In flight: {DisplayText(gameplayCommandDispatcher.InFlightRequestType, "--")} id={DisplayText(gameplayCommandDispatcher.InFlightLocalRequestId, "--")}");
        }

        if (!string.IsNullOrWhiteSpace(gameplayCommandDispatcher.LastCommandResultSummary))
        {
            lines.Add($"Result: {gameplayCommandDispatcher.LastCommandResultSummary}");
        }

        if (!string.IsNullOrWhiteSpace(gameplayCommandDispatcher.LastCommandError))
        {
            lines.Add($"Error: {gameplayCommandDispatcher.LastCommandError}");
        }

        if (gameplayCommandDispatcher.IsWaitingForAuthoritativeSnapshot)
        {
            lines.Add("Waiting: authoritative snapshot");
        }

        string disabledReason = CurrentDisabledReason();
        if (!string.IsNullOrWhiteSpace(disabledReason))
        {
            lines.Add($"Disabled: {disabledReason}");
        }

        if (hasActiveAuctionFromHydration)
        {
            lines.Add("Status: active auction");
        }

        commandFeedbackText.text = string.Join("\n", lines);
    }

    private string CurrentDisabledReason()
    {
        if (!UseLiveCommandMode())
        {
            return "";
        }

        if (!hasSnapshot || !lastSnapshot.HasRolledThisTurn)
        {
            return string.IsNullOrWhiteSpace(lastRollBlockedReason) ? "" : $"Roll - {lastRollBlockedReason}";
        }

        if (!lastSnapshot.HasResolvedTileThisTurn)
        {
            return string.IsNullOrWhiteSpace(lastResolveBlockedReason) ? "" : $"Resolve - {lastResolveBlockedReason}";
        }

        if (!lastSnapshot.HasExecutedTileThisTurn)
        {
            return string.IsNullOrWhiteSpace(lastExecuteBlockedReason) ? "" : $"Execute - {lastExecuteBlockedReason}";
        }

        if (!string.IsNullOrWhiteSpace(lastEndTurnBlockedReason))
        {
            return $"End turn - {lastEndTurnBlockedReason}";
        }

        if (!string.IsNullOrWhiteSpace(lastRollBlockedReason))
        {
            return $"Roll - {lastRollBlockedReason}";
        }

        if (!string.IsNullOrWhiteSpace(lastResolveBlockedReason))
        {
            return $"Resolve - {lastResolveBlockedReason}";
        }

        if (!string.IsNullOrWhiteSpace(lastExecuteBlockedReason))
        {
            return $"Execute - {lastExecuteBlockedReason}";
        }

        if (!string.IsNullOrWhiteSpace(lastEndTurnBlockedReason))
        {
            return $"End turn - {lastEndTurnBlockedReason}";
        }

        return "";
    }

    private string CurrentPlayerId()
    {
        return hasSnapshot ? DisplayText(lastSnapshot.CurrentPlayerId, "--") : "--";
    }

    private string TurnIndexText()
    {
        return hasSnapshot ? lastSnapshot.TurnIndex.ToString() : "--";
    }

    private string FinalMovementTileId()
    {
        return lastMovementPath == null || lastMovementPath.Length == 0
            ? (hasHudPlayerSnapshot ? DisplayText(lastHudPlayerSnapshot.CurrentTileId, "--") : "--")
            : DisplayText(lastMovementPath[lastMovementPath.Length - 1], "--");
    }

    private static string DisplayText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int Sum(int[] values)
    {
        if (values == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < values.Length; i++)
        {
            total += values[i];
        }

        return total;
    }

    private static string FormatRoll(int[] values)
    {
        if (values == null || values.Length == 0)
        {
            return "--";
        }

        return string.Join(" + ", values);
    }

    private static string FormatPath(IReadOnlyList<string> path)
    {
        if (path == null || path.Count == 0)
        {
            return "--";
        }

        return string.Join(" -> ", path);
    }
}
