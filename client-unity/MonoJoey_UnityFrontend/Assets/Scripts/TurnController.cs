using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class TurnController : MonoBehaviour
{
    [Header("Turn UI")]
    [SerializeField] private Text currentPlayerText;
    [SerializeField] private Text turnStatusText;
    [SerializeField] private Button rollButton;
    [SerializeField] private Image[] diceImages;
    [SerializeField] private Text debugLog;

    private DiceAnimator diceAnimator;
    private HUDController.TurnHudSnapshot lastSnapshot;
    private bool hasSnapshot;
    private bool isTurnActive;
    private bool hasRolledLocally;
    private int[] lastRollValues = Array.Empty<int>();

    public HUDController.TurnHudSnapshot LastSnapshot => lastSnapshot;
    public bool HasSnapshot => hasSnapshot;
    public bool IsTurnActive => isTurnActive;
    public bool HasRolledLocally => hasRolledLocally;
    public int[] LastRollValues => lastRollValues;
    public int DiceImageCount => diceImages == null ? 0 : diceImages.Length;
    public bool RollButtonInteractable => rollButton != null && rollButton.interactable;

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

        RefreshUi();
    }

    private void OnDestroy()
    {
        if (rollButton != null)
        {
            rollButton.onClick.RemoveListener(RollDice);
        }
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

    public void StartTurn()
    {
        isTurnActive = true;
        hasRolledLocally = hasSnapshot && lastSnapshot.HasRolledThisTurn;
        RefreshUi();
        AppendDebugLog($"StartTurn local UI state: player={CurrentPlayerId()}, turn={TurnIndexText()}.");
    }

    public void EndTurn()
    {
        isTurnActive = false;
        RefreshUi();
        AppendDebugLog($"EndTurn local UI state: player={CurrentPlayerId()}, lastRoll={FormatRoll(lastRollValues)}.");
    }

    public void RollDice()
    {
        if (!isTurnActive)
        {
            AppendDebugLog("Roll ignored: no local active turn.");
            return;
        }

        if (hasRolledLocally)
        {
            AppendDebugLog("Roll ignored: local turn already rolled.");
            return;
        }

        lastRollValues = new[]
        {
            UnityEngine.Random.Range(1, 7),
            UnityEngine.Random.Range(1, 7),
        };
        hasRolledLocally = true;

        if (diceAnimator != null)
        {
            diceAnimator.AnimateRoll(lastRollValues);
        }

        RefreshUi();
        AppendDebugLog($"Local dice roll: {FormatRoll(lastRollValues)}. Read-only snapshot preserved; no backend mutation.");
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
            string rollState = hasRolledLocally ? "rolled" : "ready";
            string activeState = isTurnActive ? "active" : "idle";
            turnStatusText.text = $"Turn {turnIndex}: {phase} | {activeState} | {rollState}";
        }

        if (rollButton != null)
        {
            rollButton.interactable = isTurnActive && !hasRolledLocally;
        }
    }

    private void AppendDebugLog(string message)
    {
        if (debugLog == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        debugLog.text = string.IsNullOrWhiteSpace(debugLog.text) ? message : $"{debugLog.text}\n{message}";
        Debug.Log($"[TurnController] {message}", this);
    }

    private string CurrentPlayerId()
    {
        return hasSnapshot ? DisplayText(lastSnapshot.CurrentPlayerId, "--") : "--";
    }

    private string TurnIndexText()
    {
        return hasSnapshot ? lastSnapshot.TurnIndex.ToString() : "--";
    }

    private static string DisplayText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string FormatRoll(int[] values)
    {
        if (values == null || values.Length == 0)
        {
            return "--";
        }

        return string.Join(" + ", values);
    }
}
