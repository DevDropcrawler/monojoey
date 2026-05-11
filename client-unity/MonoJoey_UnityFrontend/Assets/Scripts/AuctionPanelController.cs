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

    public event Action<BidRequest> BidRequested;

    public string AuctionId => auctionId;
    public int CurrentHighBid => currentHighBid;
    public string HighBidderPlayerId => highBidderPlayerId;
    public string ActivePlayerId => activePlayerId;
    public float RemainingSeconds => remainingSeconds;
    public BidRequest? LastBidRequest { get; private set; }

    private Coroutine highBidderPulseCoroutine;

    private void Awake()
    {
        if (bidButton != null)
        {
            bidButton.onClick.AddListener(SubmitLocalBidRequest);
        }

        RefreshSnapshotText();
        AnimateCountdownHighlight(remainingSeconds, 30f);
    }

    private void OnDestroy()
    {
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

        RefreshSnapshotText();
        AnimateCountdownHighlight(remainingSeconds, 30f);
        PulseBidHighlight(highBidderPlayerId);
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

    public void SubmitLocalBidRequest()
    {
        if (bidInput == null)
        {
            AppendLog("Bid request ignored: bid input is not wired.");
            return;
        }

        if (!int.TryParse(bidInput.text, out int amount) || amount <= 0)
        {
            AppendLog($"Invalid local bid: {bidInput.text}");
            return;
        }

        LastBidRequest = new BidRequest(auctionId, amount);
        AppendLog($"Local bid request: {auctionId} ${amount}");
        BidRequested?.Invoke(LastBidRequest.Value);
    }

    private void RefreshSnapshotText()
    {
        if (timerText != null)
        {
            timerText.text = $"{Mathf.CeilToInt(remainingSeconds)}s";
        }

        if (currentBidText != null)
        {
            currentBidText.text = $"Current Bid: ${currentHighBid}";
        }

        if (activePlayerText != null)
        {
            activePlayerText.text = $"Active: {DisplayPlayerId(activePlayerId)}";
        }

        if (highBidderText != null)
        {
            highBidderText.text = $"High Bidder: {DisplayPlayerId(highBidderPlayerId)}";
        }
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
}
