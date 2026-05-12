using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class AuctionPanelController : MonoBehaviour
{
    [Serializable]
    public sealed class PlayerBidRow
    {
        [SerializeField] private string playerId = "player-1";
        [SerializeField] private Text playerLabel;
        [SerializeField] private Text bidLabel;
        [SerializeField] private Image highlightImage;
        [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.22f, 0.85f);
        [SerializeField] private Color highlightedColor = new Color(0.20f, 0.55f, 0.85f, 0.95f);

        public string PlayerId => playerId;

        public void Bind(string id, int bid, bool highlighted)
        {
            playerId = id;

            if (playerLabel != null)
            {
                playerLabel.text = string.IsNullOrWhiteSpace(playerId) ? "Player" : playerId;
            }

            if (bidLabel != null)
            {
                bidLabel.text = bid > 0 ? $"${bid}" : "--";
            }

            SetHighlighted(highlighted);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (highlightImage != null)
            {
                highlightImage.color = highlighted ? highlightedColor : normalColor;
            }
        }

        public void SetPulseAmount(float amount)
        {
            if (highlightImage != null)
            {
                highlightImage.color = Color.Lerp(normalColor, highlightedColor, Mathf.Clamp01(amount));
            }
        }
    }

    public readonly struct BidRequest
    {
        public BidRequest(string auctionId, int amount)
        {
            AuctionId = auctionId;
            Amount = amount;
        }

        public string AuctionId { get; }
        public int Amount { get; }
    }

    [Header("Auction Snapshot")]
    [SerializeField] private string auctionId = "auction-demo";
    [SerializeField] private int currentHighBid;
    [SerializeField] private string highBidderPlayerId = "";
    [SerializeField] private string activePlayerId = "player-1";
    [SerializeField] private float remainingSeconds = 30f;
    [SerializeField] private List<PlayerBidRow> playerRows = new List<PlayerBidRow>();

    [Header("Controls")]
    [SerializeField] private InputField bidInput;
    [SerializeField] private Button bidButton;
    [SerializeField] private Text timerText;
    [SerializeField] private Text currentBidText;
    [SerializeField] private Text activePlayerText;
    [SerializeField] private Text highBidderText;
    [SerializeField] private Text logText;

    [Header("UI Animation")]
    [SerializeField] private Image timerHighlightImage;
    [SerializeField] private Image highBidderPulseImage;
    [SerializeField] private Color calmTimerColor = new Color(0.20f, 0.55f, 0.85f, 0.35f);
    [SerializeField] private Color urgentTimerColor = new Color(0.95f, 0.24f, 0.18f, 0.75f);
    [SerializeField] private float countdownPulseSeconds = 0.35f;

    [Header("Live Gameplay Commands")]
    [SerializeField] private bool useLiveGameplayCommandDispatcher;
    [SerializeField] private MonoJoeyGameplayCommandDispatcher gameplayCommandDispatcher;

    public event Action<BidRequest> BidRequested;

    public string AuctionId => auctionId;
    public int CurrentHighBid => currentHighBid;
    public string HighBidderPlayerId => highBidderPlayerId;
    public string ActivePlayerId => activePlayerId;
    public float RemainingSeconds => remainingSeconds;
    public BidRequest? LastBidRequest { get; private set; }
    public bool UseLiveGameplayCommandDispatcher => useLiveGameplayCommandDispatcher;
    public bool BidButtonInteractable => bidButton != null && bidButton.interactable;
    public bool HasActiveAuctionSnapshot => !string.IsNullOrWhiteSpace(auctionId);
    public string LastBidBlockedReason { get; private set; } = "";
    public string LastBidFeedbackText { get; private set; } = "";
    public string CurrentBidDisplayText => currentBidText == null ? "" : currentBidText.text;
    public string ActivePlayerDisplayText => activePlayerText == null ? "" : activePlayerText.text;
    public string HighBidderDisplayText => highBidderText == null ? "" : highBidderText.text;
    public string TimerDisplayText => timerText == null ? "" : timerText.text;
    public bool HasRuntimeVisualHierarchy => timerText != null && timerText.fontSize >= 24 && currentBidText != null && currentBidText.fontSize >= 21;

    private Coroutine highBidderPulseCoroutine;
    private string lastLoggedBidFeedback = "";

    private void Awake()
    {
        ApplyRuntimeVisualHierarchy();
        if (bidButton != null)
        {
            bidButton.onClick.AddListener(SubmitLocalBidRequest);
        }

        RefreshButtonLabel();
        RefreshButtonVisual();
        RefreshSnapshotText();
        RefreshBidButton();
        AnimateCountdownHighlight(remainingSeconds, 30f);
    }

    private void OnDestroy()
    {
        UnsubscribeDispatcherFeedback();

        if (bidButton != null)
        {
            bidButton.onClick.RemoveListener(SubmitLocalBidRequest);
        }
    }

    public void BindAuctionSnapshot(
        string snapshotAuctionId,
        int snapshotCurrentHighBid,
        string snapshotHighBidderPlayerId,
        string snapshotActivePlayerId,
        float snapshotRemainingSeconds)
    {
        auctionId = snapshotAuctionId;
        currentHighBid = snapshotCurrentHighBid;
        highBidderPlayerId = snapshotHighBidderPlayerId;
        activePlayerId = snapshotActivePlayerId;
        remainingSeconds = Mathf.Max(0f, snapshotRemainingSeconds);
        if (LastBidFeedbackText.StartsWith("Bid sent:", StringComparison.Ordinal))
        {
            LastBidFeedbackText = "";
        }

        RefreshSnapshotText();
        RefreshBidButton();
        AnimateCountdownHighlight(remainingSeconds, 30f);
        PulseBidHighlight(highBidderPlayerId);
    }

    public void ConfigureLiveGameplayCommandDispatcher(MonoJoeyGameplayCommandDispatcher dispatcher)
    {
        UnsubscribeDispatcherFeedback();
        gameplayCommandDispatcher = dispatcher;
        useLiveGameplayCommandDispatcher = dispatcher != null;
        SubscribeDispatcherFeedback();
        RefreshBidButton();
        SetBidFeedback("Ready to place a bid", $"Live command dispatcher configured: enabled={useLiveGameplayCommandDispatcher}");
    }

    public void BindPlayerBidRows(IReadOnlyList<string> playerIds, IReadOnlyList<int> bids)
    {
        int count = Mathf.Min(playerRows.Count, playerIds == null ? 0 : playerIds.Count);

        for (int i = 0; i < count; i++)
        {
            int bid = bids != null && i < bids.Count ? bids[i] : 0;
            string rowPlayerId = playerIds[i];
            playerRows[i].Bind(rowPlayerId, bid, rowPlayerId == activePlayerId || rowPlayerId == highBidderPlayerId);
        }

        for (int i = count; i < playerRows.Count; i++)
        {
            playerRows[i].Bind("", 0, false);
        }
    }

    public void SetPlayerHighlight(string playerId, bool highlighted)
    {
        foreach (PlayerBidRow row in playerRows)
        {
            if (row.PlayerId == playerId)
            {
                row.SetHighlighted(highlighted);
                return;
            }
        }
    }

    public void AnimateCountdownHighlight(float remainingSeconds, float totalSeconds)
    {
        if (timerHighlightImage == null)
        {
            return;
        }

        float normalizedRemaining = totalSeconds <= 0f ? 0f : Mathf.Clamp01(remainingSeconds / totalSeconds);
        float urgency = 1f - normalizedRemaining;
        timerHighlightImage.color = Color.Lerp(calmTimerColor, urgentTimerColor, urgency);
    }

    public void PulseBidHighlight(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId) && highBidderPulseImage == null)
        {
            return;
        }

        if (highBidderPulseCoroutine != null)
        {
            StopCoroutine(highBidderPulseCoroutine);
        }

        highBidderPulseCoroutine = StartCoroutine(PulseBidHighlightRoutine(playerId));
    }

    public void AppendLog(string message)
    {
        if (logText == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        logText.text = string.IsNullOrWhiteSpace(logText.text) ? message : $"{logText.text}\n{message}";
    }

    public void SetBidInputTextForValidation(string value)
    {
        if (bidInput != null)
        {
            bidInput.text = value ?? "";
        }

        RefreshBidButton();
    }

    public void SubmitLocalBidRequest()
    {
        if (bidInput == null)
        {
            LastBidBlockedReason = "bid input is not wired";
            SetBidFeedback("Bid unavailable - Enter a valid bid", $"Bid blocked: {LastBidBlockedReason}.");
            return;
        }

        if (!int.TryParse(bidInput.text, out int amount) || amount <= 0)
        {
            LastBidBlockedReason = $"invalid bid amount {DisplayPlayerId(bidInput.text)}";
            SetBidFeedback("Bid unavailable - Enter a valid bid", $"Bid blocked: {LastBidBlockedReason}.");
            RefreshBidButton();
            return;
        }

        if (UseLiveCommandMode())
        {
            if (!CanSubmitBidLive(amount, out string reason))
            {
                LastBidBlockedReason = reason;
                SetBidFeedback($"Bid unavailable - {PlayerFacingBidReason(reason)}", $"Bid blocked: {reason}.");
                RefreshBidButton();
                return;
            }

            bool sent = gameplayCommandDispatcher.TryPlaceBid(amount);
            LastBidBlockedReason = sent ? "" : gameplayCommandDispatcher.LastCommandError;
            SetBidFeedback(sent
                ? $"Bid sent: ${amount}. Waiting for server update."
                : $"Bid unavailable - {PlayerFacingBidReason(gameplayCommandDispatcher.LastCommandError)}",
                sent
                    ? $"Bid sent: ${amount}, id={DisplayPlayerId(gameplayCommandDispatcher.LastCommandLocalRequestId)}."
                    : $"Bid rejected: {gameplayCommandDispatcher.LastCommandError}");
            RefreshBidButton();
            return;
        }

        LastBidRequest = new BidRequest(auctionId, amount);
        LastBidBlockedReason = "";
        SetBidFeedback($"Bid ready: ${amount}", $"Local bid request: {auctionId} ${amount}");
        BidRequested?.Invoke(LastBidRequest.Value);
        RefreshBidButton();
    }

    private void RefreshSnapshotText()
    {
        if (timerText != null)
        {
            timerText.text = $"{Mathf.CeilToInt(remainingSeconds)}s left";
        }

        if (currentBidText != null)
        {
            currentBidText.text = $"Auction: {DisplayPlayerId(auctionId)} - current bid ${currentHighBid}";
        }

        if (activePlayerText != null)
        {
            activePlayerText.text = BuildBidPromptText();
        }

        if (highBidderText != null)
        {
            highBidderText.text = $"High bidder: {DisplayPlayerId(highBidderPlayerId)}";
        }

        RefreshBidButton();
    }

    private void Update()
    {
        RefreshBidButton();
    }

    private void RefreshBidButton()
    {
        RefreshButtonLabel();
        RefreshButtonVisual();

        if (bidButton == null)
        {
            return;
        }

        if (!UseLiveCommandMode())
        {
            bidButton.interactable = true;
            LastBidBlockedReason = "";
            LastBidFeedbackText = BuildBidPromptText();
            RefreshSnapshotTextOnlyPrompt();
            RefreshButtonVisual();
            return;
        }

        int amount = 0;
        bool hasValidAmount = bidInput != null && int.TryParse(bidInput.text, out amount) && amount > 0;
        string reason = "";
        bidButton.interactable = hasValidAmount && CanSubmitBidLive(amount, out reason);
        LastBidBlockedReason = bidButton.interactable ? "" : (hasValidAmount ? reason : InvalidAmountReason());

        if (gameplayCommandDispatcher != null
            && string.Equals(gameplayCommandDispatcher.LastCommandRequestType, MonoJoeyTransportMessageTypes.PlaceBid, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(gameplayCommandDispatcher.LastCommandError))
        {
            SetBidFeedback($"Bid unavailable - {PlayerFacingBidReason(gameplayCommandDispatcher.LastCommandError)}", $"Bid backend error: {gameplayCommandDispatcher.LastCommandError}");
            RefreshSnapshotTextOnlyPrompt();
            RefreshButtonVisual();
            return;
        }

        if (!string.IsNullOrWhiteSpace(LastBidBlockedReason))
        {
            LastBidFeedbackText = $"Bid unavailable - {PlayerFacingBidReason(LastBidBlockedReason)}";
        }
        else if (gameplayCommandDispatcher != null && gameplayCommandDispatcher.IsCommandInFlight)
        {
            LastBidFeedbackText = "Bid unavailable - Waiting for server";
        }
        else if (!string.IsNullOrWhiteSpace(LastBidFeedbackText)
            && LastBidFeedbackText.StartsWith("Bid sent:", StringComparison.Ordinal))
        {
            // Keep the sent feedback visible until the authoritative snapshot refreshes it.
        }
        else
        {
            LastBidFeedbackText = BuildBidPromptText();
        }

        RefreshSnapshotTextOnlyPrompt();
        RefreshButtonVisual();
    }

    private bool CanSubmitBidLive(int amount, out string reason)
    {
        if (gameplayCommandDispatcher == null)
        {
            reason = "live command dispatcher is not configured";
            return false;
        }

        if (!gameplayCommandDispatcher.IsGameplayCommandModeAvailable)
        {
            reason = "not live or mock command mode";
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

        if (!HasActiveAuctionSnapshot)
        {
            reason = "no active auction in authoritative snapshot";
            return false;
        }

        if (!gameplayCommandDispatcher.CanPlaceBid(amount, out reason))
        {
            return false;
        }

        reason = "";
        return true;
    }

    private IEnumerator PulseBidHighlightRoutine(string playerId)
    {
        PlayerBidRow pulsedRow = FindPlayerRow(playerId);
        Color originalPulseColor = highBidderPulseImage == null ? Color.clear : highBidderPulseImage.color;
        Color pulseColor = new Color(0.95f, 0.78f, 0.24f, 0.55f);
        float duration = Mathf.Max(0.05f, countdownPulseSeconds);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(t * Mathf.PI);

            if (highBidderPulseImage != null)
            {
                highBidderPulseImage.color = Color.Lerp(originalPulseColor, pulseColor, pulse);
            }

            pulsedRow?.SetPulseAmount(pulse);
            yield return null;
        }

        if (highBidderPulseImage != null)
        {
            highBidderPulseImage.color = originalPulseColor;
        }

        if (pulsedRow != null)
        {
            pulsedRow.SetHighlighted(pulsedRow.PlayerId == activePlayerId || pulsedRow.PlayerId == highBidderPlayerId);
        }

        highBidderPulseCoroutine = null;
    }

    private PlayerBidRow FindPlayerRow(string playerId)
    {
        foreach (PlayerBidRow row in playerRows)
        {
            if (row.PlayerId == playerId)
            {
                return row;
            }
        }

        return null;
    }

    private static string DisplayPlayerId(string playerId)
    {
        return string.IsNullOrWhiteSpace(playerId) ? "--" : playerId;
    }

    private string InvalidAmountReason()
    {
        string value = bidInput == null ? "" : bidInput.text;
        return $"invalid bid amount {DisplayPlayerId(value)}";
    }

    private void SetBidFeedback(string message, string diagnosticMessage = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        LastBidFeedbackText = message;
        RefreshSnapshotTextOnlyPrompt();

        string logMessage = string.IsNullOrWhiteSpace(diagnosticMessage) ? message : diagnosticMessage;
        if (!string.Equals(lastLoggedBidFeedback, logMessage, StringComparison.Ordinal))
        {
            AppendLog(logMessage);
            lastLoggedBidFeedback = logMessage;
        }
    }

    private void RefreshButtonLabel()
    {
        if (bidButton == null)
        {
            return;
        }

        Text text = bidButton.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = "Place bid";
        }
    }

    private void ApplyRuntimeVisualHierarchy()
    {
        if (timerText != null)
        {
            timerText.fontSize = Mathf.Max(timerText.fontSize, 24);
            timerText.fontStyle = FontStyle.Bold;
            timerText.color = PlaceholderVisualTheme.PanelAccent;
        }

        if (currentBidText != null)
        {
            currentBidText.fontSize = Mathf.Max(currentBidText.fontSize, 21);
            currentBidText.fontStyle = FontStyle.Bold;
            currentBidText.color = Color.white;
        }

        if (highBidderText != null)
        {
            highBidderText.fontSize = Mathf.Max(highBidderText.fontSize, 18);
            highBidderText.color = PlaceholderVisualTheme.SecondaryText;
        }

        if (activePlayerText != null)
        {
            activePlayerText.fontSize = Mathf.Max(activePlayerText.fontSize, 18);
            activePlayerText.color = PlaceholderVisualTheme.SecondaryText;
        }

        if (logText != null)
        {
            logText.color = PlaceholderVisualTheme.DisabledText;
        }
    }

    private void RefreshButtonVisual()
    {
        if (bidButton == null)
        {
            return;
        }

        Image image = bidButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = bidButton.interactable
                ? new Color(0.46f, 0.32f, 0.14f, 0.98f)
                : new Color(0.16f, 0.18f, 0.20f, 0.72f);
        }

        Text text = bidButton.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.fontStyle = FontStyle.Bold;
            text.color = bidButton.interactable ? Color.white : PlaceholderVisualTheme.DisabledText;
        }
    }

    private void RefreshSnapshotTextOnlyPrompt()
    {
        if (activePlayerText != null)
        {
            activePlayerText.text = string.IsNullOrWhiteSpace(LastBidFeedbackText)
                ? BuildBidPromptText()
                : LastBidFeedbackText;
        }
    }

    private string BuildBidPromptText()
    {
        if (!HasActiveAuctionSnapshot)
        {
            return "No active auction";
        }

        int minimumBid = Mathf.Max(1, currentHighBid + 1);
        return $"Bid at least ${minimumBid}";
    }

    private static string PlayerFacingBidReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "Waiting for server";
        }

        string lower = reason.ToLowerInvariant();
        if (lower.Contains("not connected")
            || lower.Contains("not live")
            || lower.Contains("dispatcher is not configured")
            || lower.Contains("not bound")
            || lower.Contains("session"))
        {
            return "Not connected";
        }

        if (lower.Contains("already in flight") || lower.Contains("command in flight"))
        {
            return "Waiting for server";
        }

        if (lower.Contains("no active auction"))
        {
            return "No active auction";
        }

        if (lower.Contains("invalid bid") || lower.Contains("amount must be positive"))
        {
            return "Enter a valid bid";
        }

        int messageIndex = reason.IndexOf("message=", StringComparison.OrdinalIgnoreCase);
        if (messageIndex >= 0)
        {
            return reason.Substring(messageIndex + "message=".Length);
        }

        return reason.Trim();
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
        RefreshBidButton();
    }

    private bool UseLiveCommandMode()
    {
        return useLiveGameplayCommandDispatcher && gameplayCommandDispatcher != null;
    }
}
